"""
OnAirCut OCR Engine — Standalone Bengali text recognition using EasyOCR.
Usage: python ocr_engine.py <image_path>
Output: Detected text lines to stdout (one per line).
First run downloads the Bengali model (~50MB) to ~/.EasyOCR/model/
"""

import sys
import os

def main():
    if len(sys.argv) < 2:
        print("Usage: python ocr_engine.py <image_path>", file=sys.stderr)
        sys.exit(1)

    image_path = sys.argv[1]
    if not os.path.exists(image_path):
        print(f"File not found: {image_path}", file=sys.stderr)
        sys.exit(1)

    try:
        import easyocr

        # Store models in the lib/ocr/models folder next to this script
        model_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "models")
        os.makedirs(model_dir, exist_ok=True)

        # Initialize reader with Bengali + English, GPU=False for standalone
        reader = easyocr.Reader(
            ['bn', 'en'],
            gpu=False,
            model_storage_directory=model_dir,
            download_enabled=True,
            verbose=False
        )

        # Read text from image
        results = reader.readtext(image_path, detail=1, paragraph=False)

        if not results:
            print("", end="")
            sys.exit(0)

        # Output: each line is "confidence|text"
        for (bbox, text, confidence) in results:
            text = text.strip()
            if text:
                print(f"{confidence:.2f}|{text}")

    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()
