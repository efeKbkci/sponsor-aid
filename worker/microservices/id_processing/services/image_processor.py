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

        # 🚀 YENİ: Çok yüksek çözünürlüklü fotoğrafları YOLO maske tensörlerini şişirmemesi için 1280px ile sınırla
        h_orig, w_orig = image.shape[:2]
        if max(h_orig, w_orig) > 1280:
            scale = 1280.0 / max(h_orig, w_orig)
            image = cv2.resize(image, (int(w_orig * scale), int(h_orig * scale)), interpolation=cv2.INTER_AREA)

        # YOLO tahminini yap (imgsz=640 ile sınırla)
        results = self.model.predict(source=image, imgsz=640, conf=0.5, save=False, verbose=False)
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
