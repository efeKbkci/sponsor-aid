import cv2
import numpy as np
import os
from ultralytics import YOLO

class ImageProcessor:
    def __init__(self, model_path: str):
        if not os.path.exists(model_path):
            raise FileNotFoundError(f"HATA: YOLO modeli bulunamadı -> {model_path}")
        
        self.model = YOLO(model_path)

    def extract_id_card(self, image_bytes: bytes) -> bytes:
        np_array = np.frombuffer(image_bytes, np.uint8)
        image = cv2.imdecode(np_array, cv2.IMREAD_COLOR)
        
        if image is None:
            raise ValueError("Görsel okunamadı veya format desteklenmiyor.")

        # YOLO tahminini yap
        results = self.model.predict(source=image, conf=0.5, save=False, verbose=False)
        result = results[0]

        # Güvenlik Kontrolü: Maske veya Bounding Box var mı?
        if result.masks is None or len(result.masks.xy) == 0:
            raise ValueError("Yapay zeka bu fotoğrafta bir kimlik kartı bulamadı.")

        # 1. Maskenin sınırlarını (poligonu) al
        polygon = result.masks.xy[0]
        cnt = np.array(polygon, dtype=np.float32)
        
        # 2. Riskli perspektif (eğip bükme) KULLANMIYORUZ!
        # Bunun yerine maskeyi tam kapsayan düz ve güvenli bir dikdörtgen alıyoruz
        x, y, w, h = cv2.boundingRect(cnt)

        # 3. Sınır dışına çıkma ihtimaline karşı güvenlik payı
        h_img, w_img = image.shape[:2]
        x = max(0, x)
        y = max(0, y)
        w = min(w_img - x, w)
        h = min(h_img - y, h)

        # 4. Resmi piksel kalitesini %100 koruyarak düz bir şekilde kes (Crop)
        cropped = image[y:y+h, x:x+w]

        # Sonucu byte formatına çevir ve yolla
        _, buffer = cv2.imencode('.jpg', cropped)
        return buffer.tobytes()

    # NOT: _order_points ve _four_point_transform metotlarını resmi bozduğu için tamamen sildik.