import cv2
import numpy as np
import easyocr
import re

class OcrService:
    def __init__(self, reader: easyocr.Reader):
        self.reader = reader

    def extract_tc_number(self, image_bytes: bytes, document_side: str) -> str:
        np_array = np.frombuffer(image_bytes, np.uint8)
        image = cv2.imdecode(np_array, cv2.IMREAD_COLOR)

        if image is None:
            raise ValueError("Görsel okunamadı.")

        h, w = image.shape[:2]
        
        # 1. ROI (İlgilenilen Bölge) KESİMİ
        if document_side.lower() == "front":
            roi = image[0:int(h * 0.35), 0:w]
        elif document_side.lower() == "back":
            roi = image[int(h * 0.65):h, 0:w]
        else:
            roi = image

        gray_roi = cv2.cvtColor(roi, cv2.COLOR_BGR2GRAY)

        # YENİ: ROI genişliği 640 pikselden büyükse orantılı küçült (Aktivasyon tensörlerini %80 küçültür)
        roi_h, roi_w = gray_roi.shape[:2]
        if roi_w > 640:
            scale = 640.0 / roi_w
            gray_roi = cv2.resize(gray_roi, (640, int(roi_h * scale)), interpolation=cv2.INTER_AREA)

        # 2. METİN OKUMA (Sadece rakamları ara ve VGG-16 tuvalini 640px ile sınırla)
        results = self.reader.readtext(
            gray_roi,
            detail=0,
            allowlist="0123456789", # Hem 1-I, 0-O gibi harf ve sayıları karıştırmaz, hem de performansı arttırır 
            canvas_size=640,
            mag_ratio=1.0
        )
        full_text = " ".join(results)

        # 3. REGEX İLE TC KİMLİK AYIKLAMA
        tc_pattern = r'\b[1-9][0-9]{10}\b'
        match = re.search(tc_pattern, full_text)

        if match:
            return match.group(0)
        return None

if __name__ == "__main__":
    import argparse
    import mimetypes
    import csv
    from pathlib import Path

    ocr_service = OcrService()
    parser = argparse.ArgumentParser()

    # Hem tekli kullanım hem de CSV kullanımı için parametreler
    parser.add_argument("-p", help="İşlenecek kimlik fotoğrafının yolu")
    parser.add_argument("-s", help="İşlenecek kimlik fotoğrafının yönü (front, back)")
    parser.add_argument("-c", "--csv", help="Toplu işlem için CSV dosyasının yolu")
    args = parser.parse_args()

    # 1. SENARYO: CSV DOSYASI VERİLMİŞSE
    if args.csv:
        csv_file = Path(args.csv)
        if not csv_file.is_file() or not csv_file.name.endswith('.csv'):
            parser.error("Hata: Geçerli bir CSV dosyası bulunamadı!")
            
        print(f"\n[*] {csv_file.name} dosyasındaki kayıtlar işleniyor...\n" + "-"*40)
        
        with open(csv_file, mode="r", encoding="utf-8") as f:
            reader = csv.reader(f)
            # Eğer CSV'nin ilk satırı başlık ise (örn: path,side) alttaki satırın yorumunu kaldır:
            # next(reader, None) 
            
            for satir_no, row in enumerate(reader, start=1):
                if len(row) < 2:
                    continue # Boş veya hatalı satırları atla
                    
                dosya_yolu = row[0].strip()
                yon = row[1].strip()
                
                img_path = Path(dosya_yolu)
                if not img_path.is_file():
                    print(f"[Satır {satir_no}] HATA: Dosya bulunamadı -> {dosya_yolu}")
                    continue
                    
                with open(img_path, "rb") as img_file:
                    image_bytes = img_file.read()
                    
                try:
                    result = ocr_service.extract_tc_number(image_bytes, yon)
                    print(f"[Satır {satir_no}] Dosya: {img_path.name} | Yön: {yon} | OCR Sonucu: {result}")
                except Exception as e:
                    print(f"[Satır {satir_no}] HATA: {img_path.name} okunamadı. Detay: {e}")
                    
    # 2. SENARYO: TEK BİR DOSYA VERİLMİŞSE (-p ve -s ile)
    elif args.p and args.s:
        file = Path(args.p)

        if not file.is_file():
            parser.error(f"Hata: '{args.p}' bulunamadı veya geçerli bir dosya değil!")

        mime_type, _ = mimetypes.guess_type(file)
        if not mime_type or not mime_type.startswith('image/'):
            parser.error("Hata: Lütfen geçerli bir fotoğraf seçin! (jpg, png, vs.)")

        if args.s not in ("front", "back"):
            parser.error("Hata: Lütfen geçerli bir kimlik yönü seçin! (front, back)")

        with open(file, "rb") as f:
            image_bytes = f.read()

        result = ocr_service.extract_tc_number(image_bytes=image_bytes, document_side=args.s)
        print(f"OCR Sonucu : {result}")
        
    # 3. SENARYO: HİÇBİR PARAMETRE GİRİLMEMİŞSE
    else:
        parser.error("Lütfen tekli işlem için '-p' ve '-s' veya toplu işlem için '-c' parametresini kullanın.")