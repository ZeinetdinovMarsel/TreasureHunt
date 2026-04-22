import socket
import json
import math
import time
from typing import Optional, Dict, Any, List

HOST = "127.0.0.1"
PORT = 8080

STATE_LOBBY = "Lobby"
STATE_INGAME = "InGame"


class SmartAgent:
    def __init__(self, agent_id):
        self.agent_id = str(agent_id)
        self.target_treasure_id = None
        self.last_cmd_time = 0.0
        self.command_interval = 0.08

    @staticmethod
    def dist(p1, p2):
        return math.sqrt((p1["x"] - p2["x"]) ** 2 + (p1["z"] - p2["z"]) ** 2)

    def can_send(self):
        now = time.time()
        if now - self.last_cmd_time < self.command_interval:
            return False
        self.last_cmd_time = now
        return True

    def decide(self, agent_state, treasures, bases, game_state, team):
        if game_state != STATE_INGAME:
            self.target_treasure_id = None
            return None

        pos = agent_state.get("pos")
        if not pos:
            return None

        if agent_state.get("isStunned"):
            self.target_treasure_id = None
            return None

        has_treasure = agent_state.get("hasTreasure", False)

        if has_treasure:
            self.target_treasure_id = None

            base = next((b for b in bases if b["team"].lower() == team.lower()), None)
            if not base:
                return None

            base_pos = base["pos"]

            if self.dist(pos, base_pos) < 2.0:
                return self._cmd("drop", base_pos)

            return self._cmd("position", base_pos)

        free = [
            t for t in treasures
            if not t.get("isPicked", False)
            and t.get("holderAgentId") is None
        ]

        if not free:
            return None

        target = None

        if self.target_treasure_id:
            target = next((t for t in free if str(t["id"]) == str(self.target_treasure_id)), None)

        if not target:
            target = min(free, key=lambda t: self.dist(pos, t["pos"]))
            self.target_treasure_id = target["id"]

        if self.dist(pos, target["pos"]) < 1.5:
            return self._cmd("pickup", target["pos"])

        return self._cmd("position", target["pos"])

    def _cmd(self, action, target=None):
        if not self.can_send():
            return None

        cmd = {
            "Id": self.agent_id,
            "action": action
        }

        if target is not None:
            cmd["target"] = target

        return cmd


def send(sock, obj):
    data = json.dumps(obj, ensure_ascii=False) + "\n"
    sock.sendall(data.encode("utf-8"))


def start_client(team_name: str):
    sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sock.settimeout(None)

    agents_ai = {}

    try:
        sock.connect((HOST, PORT))
        print(f"[Клиент] Подключился в команду {team_name}")

        send(sock, {"team": team_name})

        buffer = ""
        player_id = None

        while True:
            chunk = sock.recv(65536)
            if not chunk:
                break

            buffer += chunk.decode("utf-8", errors="ignore")

            while "\n" in buffer:
                line, buffer = buffer.split("\n", 1)
                if not line.strip():
                    continue

                msg = json.loads(line)

                if msg.get("type") == "joinAccepted":
                    player_id = msg.get("playerId")
                    print(f"[Клиент] Подключиться удалось с id пользователя {player_id}")
                    send(sock, {"actions": [{"action": "ready"}]})
                    continue

                if msg.get("type") == "joinRejected":
                    print("[Клиент] Подключение не удалось: ", msg.get("reason"))
                    return

                if msg.get("type") == "gameEvent":
                    event_type = msg.get("eventType")

                    if event_type == "start":
                        print("[Клиент] Игра началась")

                    elif event_type == "end":
                        print("[Клиент] Игра закончилась")

                    elif event_type == "result":
                        print("[Клиент] Результат: ", msg)

                    continue

                if not player_id:
                    continue

                agents = msg.get("agents", [])
                treasures = msg.get("treasures", [])
                bases = msg.get("bases", [])
                game_state = msg.get("gameState", STATE_LOBBY)

                actions = []

                for a in agents:
                    if str(a.get("team")).lower() != team_name.lower():
                        continue

                    a_id = a["agentId"]

                    if a_id not in agents_ai:
                        agents_ai[a_id] = SmartAgent(a_id)

                    cmd = agents_ai[a_id].decide(
                        a,
                        treasures,
                        bases,
                        game_state,
                        team_name
                    )

                    if cmd:
                        actions.append(cmd)

                if actions:
                    send(sock, {"actions": actions})

    except Exception as e:
        print("[Клиент ошибка]", e)

    finally:
        sock.close()
        print("[Клиент] отключился")


if __name__ == "__main__":
    start_client("blue")