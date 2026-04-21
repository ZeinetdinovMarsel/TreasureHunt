import socket

HOST = "127.0.0.1"
PORT = 8080

def main():
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.connect((HOST, PORT))
        print(f"[CONNECTED] {HOST}:{PORT}")

        buffer = ""

        while True:
            data = s.recv(4096).decode("utf-8")
            if not data:
                print("[DISCONNECTED]")
                break

            buffer += data

            while "\n" in buffer:
                line, buffer = buffer.split("\n", 1)

                if line.strip():
                    print("\n=== WORLD STATE ===")
                    print(line)


if __name__ == "__main__":
    main()