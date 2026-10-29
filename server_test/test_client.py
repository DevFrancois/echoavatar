"""
Headless EchoAvatar test: stands in for Unity + the Windows audio client.

Run it on the GPU server BEFORE starting the inference script. It:
  1. listens on MOTION_PORT (default 12347) where the inference script sends motion,
  2. once the inference script has connected, streams a WAV file to it on port 12345
     in real time (same protocol as tools/pushwav2server.py),
  3. receives the generated motion, prints progress + latency, saves it to motion_out.jsonl.

Usage:
  python server_test/test_client.py [--wav tools/015_Happy_4_x_1_0.wav] [--motion-port 12347]
"""
import argparse, json, pickle, socket, threading, time
import numpy as np
import soundfile as sf
import librosa

RATE, CHUNK = 24000, 1000

def recv_exact(sock, n):
    buf = b""
    while len(buf) < n:
        part = sock.recv(n - len(buf))
        if not part:
            return None
        buf += part
    return buf

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--wav", default="tools/015_Happy_4_x_1_0.wav")
    ap.add_argument("--server", default="127.0.0.1")
    ap.add_argument("--audio-port", type=int, default=12345)
    ap.add_argument("--motion-port", type=int, default=12347)
    ap.add_argument("--out", default="motion_out.jsonl")
    ap.add_argument("--max-seconds", type=float, default=0, help="only send the first N seconds (0 = all)")
    args = ap.parse_args()

    wav, sr = sf.read(args.wav, dtype="float32", always_2d=True)
    wav = wav.mean(axis=1)
    if sr != RATE:
        wav = librosa.resample(wav, orig_sr=sr, target_sr=RATE)
    if args.max_seconds > 0:
        wav = wav[: int(args.max_seconds * RATE)]
    print(f"[client] audio: {args.wav}, {len(wav)/RATE:.1f} s at {RATE} Hz")
    dur = len(wav) / RATE
    wav = np.concatenate([wav, np.zeros(2 * RATE, dtype=np.float32)])  # 2 s of silence to flush the last chunks

    lst = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    lst.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    lst.bind(("0.0.0.0", args.motion_port))
    lst.listen(1)
    print(f"[client] fake Unity listening on port {args.motion_port}.")
    print("[client] >>> Now start the inference script in the other terminal. Model loading can take a few minutes. <<<")
    motion_sock, addr = lst.accept()
    print(f"[client] inference script connected from {addr}")

    stats = {"msgs": 0, "frames": 0, "first_motion": None}
    t_start = {"t": None}
    out = open(args.out, "w")

    def receiver():
        while True:
            hdr = recv_exact(motion_sock, 4)
            if hdr is None:
                break
            body = recv_exact(motion_sock, int.from_bytes(hdr, "big"))
            if body is None:
                break
            msg = json.loads(body)
            now = time.time()
            if stats["first_motion"] is None and t_start["t"] is not None:
                stats["first_motion"] = now - t_start["t"]
                print(f"[client] first motion received {stats['first_motion']:.2f} s after audio started")
            n = len(msg.get("pose", []))
            stats["msgs"] += 1
            stats["frames"] += n
            out.write(json.dumps({"t": now, "pose": msg.get("pose"), "trans": msg.get("trans"),
                                  "blendshape": msg.get("blendshape")}) + "\n")
            if stats["msgs"] % 5 == 1:
                pose = np.asarray(msg.get("pose", []))
                bs = np.asarray(msg.get("blendshape", []))
                print(f"[client] chunk {stats['msgs']}: pose {pose.shape}, blendshape {bs.shape}, "
                      f"total frames {stats['frames']}")
    rt = threading.Thread(target=receiver, daemon=True)
    rt.start()

    # The inference script only accepts audio after it has connected to us; give it a moment.
    audio_sock = None
    for _ in range(60):
        try:
            audio_sock = socket.create_connection((args.server, args.audio_port), timeout=5)
            break
        except OSError:
            time.sleep(1)
    if audio_sock is None:
        print("[client] could not connect to the audio port; is the inference script running?")
        return
    print(f"[client] streaming audio to {args.server}:{args.audio_port} in real time...")
    t_start["t"] = time.time()
    for i in range(0, len(wav), CHUNK):
        block = wav[i:i + CHUNK]
        if len(block) < CHUNK:
            block = np.pad(block, (0, CHUNK - len(block)))
        data = pickle.dumps(block.reshape(-1, 1).astype(np.float32))
        audio_sock.sendall(len(data).to_bytes(4, "big") + data)
        target = t_start["t"] + (i + CHUNK) / RATE
        time.sleep(max(0.0, target - time.time()))
    audio_sock.sendall((0).to_bytes(4, "big"))
    print("[client] audio finished, waiting for remaining motion...")
    rt.join(timeout=15)
    out.close()
    print("\n========== RESULT ==========")
    print(f"audio sent       : {dur:.1f} s")
    print(f"motion chunks    : {stats['msgs']}")
    print(f"motion frames    : {stats['frames']}  (~{stats['frames']/max(dur,1e-6):.0f} frames per second of audio)")
    if stats["first_motion"] is not None:
        print(f"first-motion lag : {stats['first_motion']:.2f} s")
    print(f"saved to         : {args.out}")
    print("SUCCESS" if stats["msgs"] > 0 else "NO MOTION RECEIVED - check the inference script's output")

if __name__ == "__main__":
    main()
