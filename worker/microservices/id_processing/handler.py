import json
import os
import traceback
import uuid
import events_pb2
from .config import SEGMENTER_MODEL_PATH, ROTATOR_MODEL_PATH
from .services.image_processor import ImageProcessor
from .services.image_rotator import ImageRotator
from .services.ocr_service import OcrService
from .services.pdf_generator import PdfGenerator

class IdProcessingHandler:
    def __init__(self, reader):
        print("[*] [ID Service] Modeller RAM'e yükleniyor...")
        self.image_processor = ImageProcessor(model_path=SEGMENTER_MODEL_PATH)
        self.image_rotator = ImageRotator(model_path=ROTATOR_MODEL_PATH)
        self.ocr_service = OcrService(reader)
        self.pdf_generator = PdfGenerator()
        print("[+] [ID Service] Hazır ve bekliyor!")

    # Artık task nesnesini doğrudan parametre alıyor
    def process_image(self, task) -> events_pb2.WorkerResultMessage:
        reg_key = task.registration_key
        file_path = task.file_path
        
        # Protobuf Enum değerini OCR için string'e çeviriyoruz
        side_str = "front" if task.image_side == events_pb2.ImageSide.IMAGE_SIDE_FRONT else "back"

        print(f"\n[ID Service] 📥 Görev: ProcessImage | Yüz: {side_str} | Anahtar: {reg_key}")
        
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
            extracted_tc = self.ocr_service.extract_tc_number(rotated_bytes, side_str)
            
            if not extracted_tc:
                if side_str == "front":
                    raise ValueError("Kimliğin ön yüzünden TC Kimlik Numarası okunamadı. Lütfen fotoğrafın net, ışıksız ve parlamasız olduğuna emin olun.")
                else:
                    raise ValueError("Kimliğin arka yüzünden TC Kimlik Numarası okunamadı. Lütfen alt kısımdaki şifreli alanın (MRZ) net olduğuna emin olun.")

            print(f"TC bulundu : {extracted_tc}")
            
            with open(file_path, "wb") as f:
                f.write(rotated_bytes)

            is_success = True

        except Exception as e:
            is_success = False
            error_message = str(e)
            print(f"   [❌] HATA: {error_message}")
            traceback.print_exc()

        extracted_data_dict = {"IdNumber": extracted_tc} if extracted_tc else {}
        doc_type_enum = events_pb2.DocumentType.DOCUMENT_TYPE_IDENTITY_FRONT if side_str == "front" else events_pb2.DocumentType.DOCUMENT_TYPE_IDENTITY_BACK

        return events_pb2.WorkerResultMessage(
            registration_key=reg_key,
            document_type=doc_type_enum,
            is_success=is_success,
            error_message=error_message if not is_success else None,
            extracted_tc_number=extracted_tc if is_success and extracted_tc else None, # JSON YERİNE BURASI GELDİ
            saved_file_path=file_path
        )

    def merge_to_pdf(self, task) -> events_pb2.WorkerResultMessage:
        reg_key = task.registration_key
        print(f"\n[ID Service] 📥 Görev: MergeToPdf | Anahtar: {reg_key}")

        try:
            with open(task.front_file_path, "rb") as f:
                front_image = f.read()
            with open(task.back_file_path, "rb") as b:
                back_image = b.read()

            print("   -> PDF oluşturuluyor...")
            generated_pdf = self.pdf_generator.generate_vertical_pdf(front_image, back_image)

            base_dir = os.path.dirname(task.front_file_path)
            unique_suffix = uuid.uuid4().hex[:8]
            pdf_filename = f"{reg_key}_Identity_{unique_suffix}.pdf"
            pdf_save_path = os.path.join(base_dir, pdf_filename)

            with open(pdf_save_path, "wb") as pdf_file:
                pdf_file.write(generated_pdf)
                
            print(f"   [✅] PDF başarıyla kaydedildi: {pdf_filename}")

            return events_pb2.WorkerResultMessage(
                registration_key=reg_key,
                document_type=events_pb2.DocumentType.DOCUMENT_TYPE_MERGED_IDENTITY,
                is_success=True,
                saved_file_path=pdf_save_path
            )

        except Exception as e:
            error_msg = str(e)
            print(f"   [❌] PDF HATA: {error_msg}")
            
            return events_pb2.WorkerResultMessage(
                registration_key=reg_key,
                document_type=events_pb2.DocumentType.DOCUMENT_TYPE_MERGED_IDENTITY,
                is_success=False,
                error_message=error_msg
            )