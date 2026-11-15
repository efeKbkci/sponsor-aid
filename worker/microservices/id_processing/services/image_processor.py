import cv2
import numpy as np
import os
from ultralytics import YOLO

class ImageProcessor:
    """Detects and isolates identity cards from raw photos using YOLO segmentation.

    Extracts an axis-aligned bounding box around the segmented card to prevent text distortion.
    """

    def __init__(self, model_path: str):
        """Load the YOLO segmentation model from the specified file path.

        Raises FileNotFoundError if the model weights cannot be located on disk.
        """
        if not os.path.exists(model_path):
            raise FileNotFoundError(f"HATA: YOLO modeli bulunamadı -> {model_path}")
        
        self.model = YOLO(model_path)

    def extract_id_card(self, image_bytes: bytes) -> bytes:
        """Locate the ID card in image bytes and crop the target area.

        Applies a confidence threshold of 0.5 to balance detection accuracy and noise rejection.
        """
        np_array = np.frombuffer(image_bytes, np.uint8)
        image = cv2.imdecode(np_array, cv2.IMREAD_COLOR)
        
        if image is None:
            raise ValueError("Görsel okunamadı veya format desteklenmiyor.")

        # Confidence threshold of 0.5 balances detecting clear card edges while discarding background noise.
        results = self.model.predict(source=image, conf=0.5, save=False, verbose=False)
        result = results[0]

        # Absence of segmentation mask confirms no recognizable ID card outline was detected.
        if result.masks is None or len(result.masks.xy) == 0:
            raise ValueError("Yapay zeka bu fotoğrafta bir kimlik kartı bulamadı.")

        # Extract polygon coordinates of the highest confidence card segmentation mask.
        polygon = result.masks.xy[0]
        cnt = np.array(polygon, dtype=np.float32)
        
        # Use an axis-aligned bounding box rather than perspective warping to avoid text distortion for OCR.
        x, y, w, h = cv2.boundingRect(cnt)

        # Clamp bounding coordinates to image dimensions to prevent out-of-bound slices.
        h_img, w_img = image.shape[:2]
        x = max(0, x)
        y = max(0, y)
        w = min(w_img - x, w)
        h = min(h_img - y, h)

        # Crop the detected region directly to preserve original pixel clarity.
        cropped = image[y:y+h, x:x+w]

        # Encode the cropped array to JPEG bytes for downstream pipeline steps.
        _, buffer = cv2.imencode('.jpg', cropped)
        return buffer.tobytes()

if __name__ == "__main__":
    import argparse
    from pathlib import Path

    parser = argparse.ArgumentParser(
        description="Kimlik fotoğrafındaki kartı kırpar ve _processed ekli yeni dosyaya kaydeder."
    )
    parser.add_argument("image_path", type=Path, help="İşlenecek kimlik fotoğrafının yolu")
    args = parser.parse_args()

    if not args.image_path.is_file():
        parser.error(f"Fotoğraf dosyası bulunamadı: {args.image_path}")

    output_path = args.image_path.with_name(
        f"{args.image_path.stem}_processed{args.image_path.suffix}"
    )
    model_path = Path(__file__).resolve().parents[1] / "models" / "id_segmenter.pt"

    try:
        image_processor = ImageProcessor(str(model_path))
        processed_image = image_processor.extract_id_card(args.image_path.read_bytes())

        if args.image_path.suffix.lower() not in (".jpg", ".jpeg"):
            decoded_image = cv2.imdecode(
                np.frombuffer(processed_image, np.uint8), cv2.IMREAD_COLOR
            )
            success, encoded_image = cv2.imencode(args.image_path.suffix, decoded_image)
            if not success:
                parser.error(f"Fotoğraf formatı desteklenmiyor: {args.image_path.suffix}")
            processed_image = encoded_image.tobytes()

        output_path.write_bytes(processed_image)
    except (OSError, ValueError) as error:
        parser.error(str(error))

    print(f"İşlenmiş fotoğraf kaydedildi: {output_path}")
