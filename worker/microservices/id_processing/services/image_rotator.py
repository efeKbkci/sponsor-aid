import cv2
import numpy as np
import os
from ultralytics import YOLO

class ImageRotator:
    # Arayüz (ABC) bağımlılığını sildik, model_path parametresini dinamik hale getirdik.
    def __init__(self, model_path: str):
        if not os.path.exists(model_path):
            raise FileNotFoundError(f"HATA: YOLO modeli bulunamadı -> {model_path}")
        self.model = YOLO(model_path)

    def rotate_id_card(self, image_bytes: bytes) -> bytes:
        np_array = np.frombuffer(image_bytes, np.uint8)
        image = cv2.imdecode(np_array, cv2.IMREAD_COLOR)
        
        if image is None:
            raise ValueError("Görsel okunamadı veya format desteklenmiyor.")
        
        # Resim dikeyse önce yatağa çevir
        h, w = image.shape[:2]
        if h > w:
            image = cv2.rotate(image, cv2.ROTATE_90_CLOCKWISE)

        # YOLO tahmini
        results = self.model.predict(source=image, save=False, verbose=False)
        result = results[0]

        predicted_index = result.probs.top1
        predicted_class = result.names[predicted_index]

        # Ters ise 180 derece düzelt
        if predicted_class.lower() == "reversed":
            image = cv2.rotate(image, cv2.ROTATE_180)
            print("DEBUG | Resim Ters Çevrildi")

        _, buffer = cv2.imencode('.jpg', image)
        return buffer.tobytes()