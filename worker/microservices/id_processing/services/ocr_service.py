import os
import asyncio
import cv2
import numpy as np
import re
import aiohttp

class OcrService:
    """Extracts citizen identity numbers from ID card images using Azure Computer Vision.

    Provides synchronous extraction by scoping OCR to targeted regions of interest.
    """

    def __init__(self):
        self.endpoint = os.environ.get("AZURE_VISION_ENDPOINT")
        self.subscription_key = os.environ.get("AZURE_VISION_KEY")

        if not self.endpoint or not self.subscription_key:
            print("⚠️ UYARI: Azure Vision API anahtarları eksik! Lütfen .env dosyasını kontrol edin.")

    def extract_tc_number(self, image_bytes: bytes, document_side: str) -> str | None:
        """Extract the 11-digit Turkish citizen ID number from the specified card side.

        Restricts OCR to the upper half for front sides and lower half for back sides.
        """
        np_array = np.frombuffer(image_bytes, np.uint8)
        image = cv2.imdecode(np_array, cv2.IMREAD_COLOR)

        if image is None:
            raise ValueError("Görsel okunamadı.")

        h, w = image.shape[:2]
        
        # Front cards place the citizen ID in the upper half; back cards place it in the lower MRZ zone.
        # Slicing to the relevant half reduces payload size and eliminates false positive number matches.
        if document_side.lower() == "front":
            roi = image[0:int(h * 0.50), 0:w]
        elif document_side.lower() == "back":
            roi = image[int(h * 0.50):h, 0:w]
        else:
            roi = image

        _, buffer = cv2.imencode('.jpg', roi)
        roi_bytes = buffer.tobytes()

        # Executes the asynchronous HTTP request inside a synchronous wrapper for caller compatibility.
        text_lines = asyncio.run(self._call_azure_read_api(roi_bytes))
        full_text = " ".join(text_lines)

        # Turkish citizen ID numbers consist of 11 digits and never start with zero.
        tc_pattern = r'\b[1-9][0-9]{10}\b'
        match = re.search(tc_pattern, full_text)

        if match:
            return match.group(0)
        return None

    async def _call_azure_read_api(self, image_bytes: bytes) -> list[str]:
        """Submit the image to Azure Read API and poll until text extraction completes.

        Returns all detected text lines from the document analysis result.
        """
        if not self.endpoint or not self.subscription_key:
            raise ValueError("Sistemde Azure OCR anahtarları bulunamadı.")

        analyze_url = f"{self.endpoint.rstrip('/')}/vision/v3.2/read/analyze"
        headers = {
            'Ocp-Apim-Subscription-Key': self.subscription_key,
            'Content-Type': 'application/octet-stream'
        }

        # Azure Read API is asynchronous: first request initiates the OCR operation and returns a status URL.
        async with aiohttp.ClientSession() as session:
            async with session.post(analyze_url, headers=headers, data=image_bytes) as response:
                response.raise_for_status()
                operation_url = response.headers.get("Operation-Location")
            
            if not operation_url:
                raise ValueError("Azure API'den operasyon doğrulama linki alınamadı.")

            poll_headers = {'Ocp-Apim-Subscription-Key': self.subscription_key}
            
            # Poll operation URL with 0.5s intervals until Azure completes document recognition.
            while True:
                await asyncio.sleep(0.5)
                async with session.get(operation_url, headers=poll_headers) as poll_response:
                    poll_response.raise_for_status()
                    result = await poll_response.json()

                    if result.get("status") == "succeeded":
                        lines = []
                        for read_result in result.get("analyzeResult", {}).get("readResults", []):
                            for line in read_result.get("lines", []):
                                lines.append(line.get("text", ""))
                        return lines
                        
                    elif result.get("status") == "failed":
                        raise ValueError("Azure OCR okuma işlemini başaramadı.")

if __name__ == "__main__":
    import argparse
    import mimetypes
    import csv
    from pathlib import Path

    # Load environment variables from .env file for local testing.
    from dotenv import load_dotenv, find_dotenv
    
    # Traverse directories to locate the closest .env file.
    load_dotenv(find_dotenv())

    ocr_service = OcrService()
    parser = argparse.ArgumentParser()

    # Support single-file execution and bulk processing via CSV.
    parser.add_argument("-p", help="İşlenecek kimlik fotoğrafının yolu")
    parser.add_argument("-s", help="İşlenecek kimlik fotoğrafının yönü (front, back)")
    parser.add_argument("-c", "--csv", help="Toplu işlem için CSV dosyasının yolu")
    args = parser.parse_args()

    # Scenario 1: Batch execution using a CSV manifest.
    if args.csv:
        csv_file = Path(args.csv)
        if not csv_file.is_file() or not csv_file.name.endswith('.csv'):
            parser.error("Hata: Geçerli bir CSV dosyası bulunamadı!")
            
        print(f"\n[*] {csv_file.name} dosyasındaki kayıtlar işleniyor...\n" + "-"*40)
        
        with open(csv_file, mode="r", encoding="utf-8") as f:
            reader = csv.reader(f)
            # Skip header line if present in CSV file.
            # next(reader, None) 
            
            for satir_no, row in enumerate(reader, start=1):
                if len(row) < 2:
                    continue  # Skip empty or incomplete rows.
                    
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
                    
    # Scenario 2: Single file execution with path and side options.
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
        
    # Scenario 3: No required CLI arguments provided.
    else:
        parser.error("Lütfen tekli işlem için '-p' ve '-s' veya toplu işlem için '-c' parametresini kullanın.")
