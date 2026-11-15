import cv2
import numpy as np
import os
from ultralytics import YOLO

class ImageRotator:
    """Corrects orientation of cropped ID card images using a YOLO classification model.

    Ensures text is horizontally aligned and right-side up before sending images to OCR.
    """

    def __init__(self, model_path: str):
        """Load the orientation classification model from disk.

        Accepts the model path as a parameter to support different deployment environments.
        """
        if not os.path.exists(model_path):
            raise FileNotFoundError(f"HATA: YOLO modeli bulunamadı -> {model_path}")
        self.model = YOLO(model_path)

    def rotate_id_card(self, image_bytes: bytes) -> bytes:
        """Rotate the card image into a standard landscape, upright orientation.

        Applies 90-degree and 180-degree rotations based on aspect ratio and model predictions.
        """
        np_array = np.frombuffer(image_bytes, np.uint8)
        image = cv2.imdecode(np_array, cv2.IMREAD_COLOR)
        
        if image is None:
            raise ValueError("Görsel okunamadı veya format desteklenmiyor.")
        
        # Standard ID cards have landscape orientation, so portrait images are rotated 90 degrees.
        h, w = image.shape[:2]
        if h > w:
            image = cv2.rotate(image, cv2.ROTATE_90_CLOCKWISE)

        # Classify whether the landscape image is upright or reversed.
        results = self.model.predict(source=image, save=False, verbose=False)
        result = results[0]

        predicted_index = result.probs.top1
        predicted_class = result.names[predicted_index]

        # Rotate 180 degrees if upside-down so text reads correctly during OCR.
        if predicted_class.lower() == "reversed":
            image = cv2.rotate(image, cv2.ROTATE_180)
            print("DEBUG | Resim Ters Çevrildi")

        _, buffer = cv2.imencode('.jpg', image)
        return buffer.tobytes()
