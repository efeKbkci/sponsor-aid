import json
import os
import traceback
import uuid
from .config import SEGMENTER_MODEL_PATH, ROTATOR_MODEL_PATH
from .services.image_processor import ImageProcessor
from .services.image_rotator import ImageRotator
from .services.ocr_service import OcrService
from .services.pdf_generator import PdfGenerator

class IdProcessingHandler:
    def __init__(self):
        print("[*] [ID Service] Modeller RAM'e yükleniyor...")
        self.image_processor = ImageProcessor(model_path=SEGMENTER_MODEL_PATH)
        self.image_rotator = ImageRotator(model_path=ROTATOR_MODEL_PATH)
        self.ocr_service = OcrService()
        self.pdf_generator = PdfGenerator()
        print("[+] [ID Service] Hazır ve bekliyor!")

    def handle_event(self, event: dict) -> dict:
        """
        Gelen olayı (event) işler ve C#'a geri gönderilecek olan
        sonuç sözlüğünü (Result Command) geri döner.
        """
        command_type = event.get('CommandType')
        reg_key = event.get('RegistrationKey')

        if command_type == "ProcessImage":
            return self._process_image(event, reg_key)
        
        elif command_type == "MergeToPdf":
            return self._merge_to_pdf(event, reg_key)

        return None

    def _process_image(self, event, reg_key):
        file_path = event.get('FilePath')
        document_side = event.get('DocumentSide', 'Front')
        
        print(f"\n[ID Service] 📥 Görev: ProcessImage | Yüz: {document_side} | Anahtar: {reg_key}")
        
        is_success = False
        error_message = None
        extracted_tc = None

        try:
            with open(file_path, "rb") as f:
                image_bytes = f.read()

            print("   -> Kırpma ve Hizalama...")
            cropped_bytes = self.image_processor.extract_id_card(image_bytes)
            rotated_bytes = self.image_rotator.rotate_id_card(cropped_bytes)

            print("   -> OCR ile TC No Analizi...")
            extracted_tc = self.ocr_service.extract_tc_number(rotated_bytes, document_side)
            
            if extracted_tc:
                print(f"   [✅] TC No Bulundu: {extracted_tc}")
            else:
                print("   [⚠️] TC No okunamadı!")

            with open(file_path, "wb") as f:
                f.write(rotated_bytes)

            is_success = True
        except Exception as e:
            is_success = False
            error_message = str(e)
            print(f"   [❌] HATA: {error_message}")
            traceback.print_exc()

        extracted_data_dict = {"IdNumber": extracted_tc} if extracted_tc else {}

        return {
            "RegistrationKey": reg_key,
            "DocumentType": "IdentityDocument", 
            "IsSuccess": is_success,
            "ErrorMessage": error_message,
            "ExtractedDataJson": json.dumps(extracted_data_dict) if is_success else None, 
            "SavedFilePath": file_path
        }

    def _merge_to_pdf(self, event, reg_key):
        print(f"\n[ID Service] 📥 Görev: MergeToPdf | Anahtar: {reg_key}")

        front_side_path = event.get("FrontFilePath")
        back_side_path = event.get("BackFilePath")

        try:
            # 1. Resimleri diskten oku
            with open(front_side_path, "rb") as f:
                front_image = f.read()
            with open(back_side_path, "rb") as b:
                back_image = b.read()

            # 2. PDF byte'larını üret
            print("   -> PDF oluşturuluyor...")
            generated_pdf = self.pdf_generator.generate_vertical_pdf(front_image, back_image)

            # 3. Kayıt yolunu belirle
            base_dir = os.path.dirname(front_side_path)
            
            # YENİ: Dosya adının sonuna rastgele 8 haneli bir harf/sayı dizisi ekliyoruz
            # Örnek Çıktı: A1B2C3_Identity_a7b8c9d0.pdf
            unique_suffix = uuid.uuid4().hex[:8]
            pdf_filename = f"{reg_key}_Identity_{unique_suffix}.pdf"
            
            pdf_save_path = os.path.join(base_dir, pdf_filename)

            # 4. PDF'i diske yaz
            with open(pdf_save_path, "wb") as pdf_file:
                pdf_file.write(generated_pdf)
                
            print(f"   [✅] PDF başarıyla kaydedildi: {pdf_filename}")

            return {
                "RegistrationKey": reg_key,
                "DocumentType": "IdentityDocument",
                "IsSuccess": True,
                "ErrorMessage": None,
                "ExtractedDataJson": None,
                "SavedFilePath": pdf_save_path  # C# bu yolu alıp veritabanına kaydedecek
            }

        except Exception as e:
            error_msg = str(e)
            print(f"   [❌] PDF HATA: {error_msg}")

            return {
                "RegistrationKey": reg_key,
                "DocumentType": "IdentityDocument",
                "IsSuccess": False,
                "ErrorMessage": error_msg,
                "ExtractedDataJson": None,
                "SavedFilePath": None
            }