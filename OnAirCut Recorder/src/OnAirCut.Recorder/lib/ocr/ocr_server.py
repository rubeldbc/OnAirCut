"""
OnAirCut OCR Server — Persistent EasyOCR process.
Runs once, loads models into memory, then accepts requests via stdin.

Protocol:
  Input:  REQ|{request_id}|{image_path}
  Output: RES|{request_id}|OK|{confidence}|{text}
          RES|{request_id}|EMPTY
          RES|{request_id}|ERROR|{message}
  Control: QUIT (exits server)
  Status:  LOADING, READY
"""
import sys
import os
import io
import warnings

def main():
    # Force UTF-8 on stdout/stderr
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', line_buffering=True)
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8', line_buffering=True)

    # Suppress all warnings from going to stdout
    warnings.filterwarnings("ignore")

    # Redirect any stray prints from libraries to stderr
    import contextlib

    script_dir = os.path.dirname(os.path.abspath(__file__))
    model_dir = os.path.join(script_dir, "models")

    try:
        # Suppress EasyOCR/PyTorch startup messages
        with contextlib.redirect_stdout(sys.stderr):
            import easyocr

        sys.stdout.write("LOADING\n")
        sys.stdout.flush()

        with contextlib.redirect_stdout(sys.stderr):
            reader = easyocr.Reader(
                ['bn', 'en'],
                gpu=False,
                model_storage_directory=model_dir,
                download_enabled=False,
                verbose=False
            )

        sys.stdout.write("READY\n")
        sys.stdout.flush()
    except Exception as e:
        sys.stdout.write(f"ERROR|{e}\n")
        sys.stdout.flush()
        sys.exit(1)

    # Main loop
    while True:
        try:
            line = sys.stdin.readline()
            if not line:  # EOF
                break

            line = line.strip()
            if not line:
                continue
            if line.upper() == "QUIT":
                break

            # Parse request: REQ|{id}|{path}
            if line.startswith("REQ|"):
                parts = line.split("|", 2)
                if len(parts) < 3:
                    continue
                req_id = parts[1]
                image_path = parts[2]
            else:
                # Legacy: plain image path
                req_id = "0"
                image_path = line

            if not os.path.exists(image_path):
                sys.stdout.write(f"RES|{req_id}|ERROR|File not found\n")
                sys.stdout.flush()
                continue

            # Run OCR with all library output redirected to stderr
            try:
                with contextlib.redirect_stdout(sys.stderr):
                    results = reader.readtext(image_path, detail=1, paragraph=False)
            except Exception as e:
                sys.stdout.write(f"RES|{req_id}|ERROR|{e}\n")
                sys.stdout.flush()
                continue

            if not results:
                sys.stdout.write(f"RES|{req_id}|EMPTY\n")
                sys.stdout.flush()
                continue

            texts = []
            total_conf = 0.0
            for (bbox, text, confidence) in results:
                text = text.strip()
                if text:
                    texts.append(text)
                    total_conf += confidence

            if texts:
                avg_conf = total_conf / len(texts)
                combined = " ".join(texts)
                # Replace any pipe characters in text to avoid protocol confusion
                combined = combined.replace("|", " ")
                sys.stdout.write(f"RES|{req_id}|OK|{avg_conf:.2f}|{combined}\n")
            else:
                sys.stdout.write(f"RES|{req_id}|EMPTY\n")

            sys.stdout.flush()

        except Exception as e:
            sys.stderr.write(f"Server error: {e}\n")
            sys.stderr.flush()

if __name__ == "__main__":
    main()
