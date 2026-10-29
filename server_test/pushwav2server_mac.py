"""
macOS version of tools/pushwav2server.py.
Differences: no 'keyboard' package (it needs root on macOS; press Ctrl+C to stop instead),
and the server IP / input device are command-line options instead of edited constants.

  pip install sounddevice numpy
  python pushwav2server_mac.py --list                     # show audio devices
  python pushwav2server_mac.py --server 192.168.1.50      # default device name: BlackHole
"""
import argparse, pickle, socket
import sounddevice as sd

ap = argparse.ArgumentParser()
ap.add_argument("--server", default="192.168.1.26", help="GPU server IP")
ap.add_argument("--port", type=int, default=12345)
ap.add_argument("--device", default="BlackHole", help="input device index or part of its name")
ap.add_argument("--list", action="store_true")
args = ap.parse_args()

if args.list:
    print(sd.query_devices())
    raise SystemExit

dev = int(args.device) if args.device.isdigit() else next(
    i for i, d in enumerate(sd.query_devices())
    if args.device.lower() in d["name"].lower() and d["max_input_channels"] > 0)
print(f"Using input device {dev}: {sd.query_devices(dev)['name']}")

RATE, CHUNK = 24000, 1000
sock = socket.create_connection((args.server, args.port))
try:
    with sd.InputStream(device=dev, channels=1, samplerate=RATE, blocksize=CHUNK) as stream:
        print("Streaming audio... press Ctrl+C to stop.")
        while True:
            data, _ = stream.read(CHUNK)
            b = pickle.dumps(data)
            sock.sendall(len(b).to_bytes(4, "big") + b)
except KeyboardInterrupt:
    sock.sendall((0).to_bytes(4, "big"))
finally:
    sock.close()
    print("Connection closed.")
