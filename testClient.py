import socket
import json
import math

HOST = '127.0.0.1'
PORT = 8080

class SmartAgent:
    def __init__(self, agent_id):
        self.agent_id = agent_id
        self.target_treasure_id = None
        self.last_destination = None

    def dist(self, p1, p2):
        return math.sqrt((p1['x'] - p2['x'])**2 + (p1['z'] - p2['z'])**2)

    def decide(self, data, treasures, bases):
        pos = data['pos']
        has_treasure = data.get('hasTreasure', False)
        team = str(data.get('team', 'blue')).lower()

        if data.get('isStunned', False):
            self.target_treasure_id = None
            self.last_destination = None
            return None

        if has_treasure:
            self.target_treasure_id = None

            my_base = next((b for b in bases if str(b['team']).lower() == team), None)

            if my_base:
                base_pos = my_base['pos']
                distance_to_base = self.dist(pos, base_pos)

                if distance_to_base < 2.0:
                    self.last_destination = None
                    print(f"[{self.agent_id}] Доставил на базу")
                    return {"id": str(self.agent_id), "action": "drop"}

                if self.last_destination != base_pos:
                    self.last_destination = base_pos
                    return {"id": str(self.agent_id), "action": "position", "target": base_pos}

            return None
        
        free_treasures = [
            t for t in treasures
            if not t.get('isPicked', False) and t.get('holderAgentId') is None
        ]

        if not free_treasures:
            return None

        target_obj = next((t for t in free_treasures if t['id'] == self.target_treasure_id), None)

        if not target_obj:
            target_obj = min(free_treasures, key=lambda t: self.dist(pos, t['pos']))
            self.target_treasure_id = target_obj['id']
            self.last_destination = None

        if self.dist(pos, target_obj['pos']) < 1.5:
            print(f"[{self.agent_id}] Подбираю {target_obj['id']}")
            self.target_treasure_id = None
            return {"id": str(self.agent_id), "action": "pickup"}

        if self.last_destination != target_obj['pos']:
            self.last_destination = target_obj['pos']
            return {
                "id": str(self.agent_id),
                "action": "position",
                "target": target_obj['pos']
            }

        return None


def main():
    agents_ai = {}

    sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    try:
        sock.connect((HOST, PORT))
        print("Подключено к Unity!")
    except Exception as e:
        print(f"Ошибка подключения: {e}")
        return

    buffer = ""

    while True:
        try:
            chunk = sock.recv(65536).decode('utf-8')
            if not chunk:
                break

            buffer += chunk

            while "\n" in buffer:
                line, buffer = buffer.split("\n", 1)

                if not line.strip():
                    continue

                try:
                    state = json.loads(line)
                except json.JSONDecodeError:
                    continue

                treasures = state.get('treasures', [])
                bases = state.get('bases', [])

                commands = []

                for a_data in state.get('agents', []):
                    a_id = a_data.get('agentId')
                    if a_id is None:
                        continue

                    if a_id not in agents_ai:
                        agents_ai[a_id] = SmartAgent(a_id)

                    cmd = agents_ai[a_id].decide(a_data, treasures, bases)

                    if cmd:
                        commands.append(cmd)

                if commands:
                    packet = json.dumps({"actions": commands}) + "\n"
                    sock.sendall(packet.encode('utf-8'))

        except Exception as e:
            print(f"Ошибка в цикле: {e}")
            break

    print("Соединение закрыто.")
    sock.close()


if __name__ == "__main__":
    main()