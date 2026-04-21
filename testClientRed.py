import socket
import json
import math
import time

HOST = '127.0.0.1'
PORT = 8080


class SmartAgent:
    def __init__(self, agent_id):
        self.agent_id = agent_id
        self.target_treasure_id = None
        self.last_pos = None
        self.last_cmd_time = 0

    def dist(self, p1, p2):
        return math.sqrt(
            (p1['x'] - p2['x']) ** 2 +
            (p1['z'] - p2['z']) ** 2
        )

    def can_send(self):
        # анти-спам (10 команд/сек максимум)
        now = time.time()
        if now - self.last_cmd_time < 0.1:
            return False
        self.last_cmd_time = now
        return True

    def decide(self, data, treasures, bases, game_state):
        if game_state != "InGame":
            return None

        pos = data['pos']
        has_treasure = data.get('hasTreasure', False)
        team = str(data.get('team', 'blue')).lower()

        if data.get('isStunned', False):
            self.target_treasure_id = None
            return None

        # -----------------------------
        # RETURN TREASURE TO BASE
        # -----------------------------
        if has_treasure:
            self.target_treasure_id = None

            my_base = next((b for b in bases if str(b['team']).lower() == team), None)
            if not my_base:
                return None

            base_pos = my_base['pos']
            dist = self.dist(pos, base_pos)

            if dist < 2.0:
                return self._cmd("drop")

            if self.last_pos != base_pos:
                self.last_pos = base_pos
                return self._cmd("position", base_pos)

            return None

        # -----------------------------
        # FIND TREASURE
        # -----------------------------
        free_treasures = [
            t for t in treasures
            if not t.get('isPicked', False) and t.get('holderAgentId') is None
        ]

        if not free_treasures:
            return None

        target = next(
            (t for t in free_treasures if t['id'] == self.target_treasure_id),
            None
        )

        if not target:
            target = min(free_treasures, key=lambda t: self.dist(pos, t['pos']))
            self.target_treasure_id = target['id']
            self.last_pos = None

        dist = self.dist(pos, target['pos'])

        if dist < 1.5:
            self.target_treasure_id = None
            return self._cmd("pickup")

        if self.last_pos != target['pos']:
            self.last_pos = target['pos']
            return self._cmd("position", target['pos'])

        return None

    def _cmd(self, action, target=None):
        if not self.can_send():
            return None

        cmd = {
            "id": str(self.agent_id),
            "action": action
        }

        if target:
            cmd["target"] = target

        return cmd



def send_register(sock, team):
    sock.sendall((json.dumps({
        "actions": [
            {"id": "2", "action": "join", "team": team}
        ]
    }) + "\n").encode())

    time.sleep(0.1)

    sock.sendall((json.dumps({
        "actions": [
            {"id": "2", "action": "ready"}
        ]
    }) + "\n").encode())
# -----------------------------
# MAIN LOOP
# -----------------------------
def main():
    agents_ai = {}

    sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)

    try:
        sock.connect((HOST, PORT))
        print("Connected to Unity server!")
        send_register(sock,"red")
    except Exception as e:
        print(f"Connection error: {e}")
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

                # ⚡ ВАЖНО: добавь gameState в Unity DTO
                game_state = state.get("gameState", "Menu")

                commands = []

                for a in state.get('agents', []):
                    a_id = a.get('agentId')
                    if a_id is None:
                        continue

                    if a_id not in agents_ai:
                        agents_ai[a_id] = SmartAgent(a_id)

                    cmd = agents_ai[a_id].decide(
                        a,
                        treasures,
                        bases,
                        game_state
                    )

                    if cmd:
                        commands.append(cmd)

                if commands:
                    packet = json.dumps({"actions": commands}) + "\n"
                    sock.sendall(packet.encode('utf-8'))

        except Exception as e:
            print(f"Loop error: {e}")
            break

    print("Disconnected.")
    sock.close()


if __name__ == "__main__":
    main()