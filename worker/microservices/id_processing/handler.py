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
    """Coordinates ID card cropping, rotation alignment, OCR extraction, and PDF merging.

    Loads computer vision models once during initialization to serve image pipeline requests.
    """

    def __init__(self):
        print("[*] [ID Service] Modeller RAM'e yükleniyor...")
        self.image_processor = ImageProcessor(model_path=SEGMENTER_MODEL_PATH)
        self.image_rotator = ImageRotator(model_path=ROTATOR_MODEL_PATH)
        self.ocr_service = OcrService() 
        self.pdf_generator = PdfGenerator()
        print("[+] [ID Service] Hazır ve bekliyor!")

    def process_image(self, task) -> events_pb2.WorkerResultMessage:
        """Process an ID card side by cropping, orienting, and extracting the citizen ID.

        Overwrites the target file with the normalized image and reports extraction status.
        """
        reg_key = task.registration_key
        file_path = task.file_path
        
        # Convert protobuf enum to string to select the correct region of interest for OCR.
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
            
            # Overwrite original image with cropped and aligned version for archiving and PDF creation.
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
            extracted_tc_number=extracted_tc if is_success and extracted_tc else None,  # Direct field provides type safety for downstream consumers.
            saved_file_path=file_path
        )

    def merge_to_pdf(self, task) -> events_pb2.WorkerResultMessage:
        """Combine normalized front and back card images into a unified A4 PDF.

        Saves the resulting PDF with a unique suffix to prevent file name collisions.
        """
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

if __name__ == "__main__":
    import argparse
    from pathlib import Path
    from dotenv import load_dotenv, find_dotenv

    parser = argparse.ArgumentParser(
        description="Ön ve arka yüz kimlik fotoğraflarını kırpar, hizalar, TC bilgisini okur ve aynı dizine <TC>_merged.pdf olarak birleştirir."
    )
    parser.add_argument("front_path", type=Path, help="Kimliğin ön yüz fotoğrafının yolu")
    parser.add_argument("back_path", type=Path, help="Kimliğin arka yüz fotoğrafının yolu")
    args = parser.parse_args()

    if not args.front_path.is_file():
        parser.error(f"Ön yüz fotoğrafı bulunamadı: {args.front_path}")
    if not args.back_path.is_file():
        parser.error(f"Arka yüz fotoğrafı bulunamadı: {args.back_path}")

    load_dotenv(find_dotenv())

    try:
        handler = IdProcessingHandler()

        print(f"[*] Ön yüz işleniyor: {args.front_path.name}")
        front_cropped = handler.image_processor.extract_id_card(args.front_path.read_bytes())
        front_rotated = handler.image_rotator.rotate_id_card(front_cropped)
        front_tc = handler.ocr_service.extract_tc_number(front_rotated, "front")

        print(f"[*] Arka yüz işleniyor: {args.back_path.name}")
        back_cropped = handler.image_processor.extract_id_card(args.back_path.read_bytes())
        back_rotated = handler.image_rotator.rotate_id_card(back_cropped)
        back_tc = handler.ocr_service.extract_tc_number(back_rotated, "back")

        if front_tc and back_tc and front_tc != back_tc:
            raise ValueError(
                f"Ön yüz ({front_tc}) ve arka yüz ({back_tc}) TC kimlik numaraları uyuşmuyor."
            )

        tc_number = front_tc or back_tc
        if not tc_number:
            raise ValueError("Kimlik fotoğraflarından TC Kimlik Numarası okunamadı.")

        if not front_tc or not back_tc:
            print(f"[⚠️] Uyarı: TC yalnızca tek yüzden okunabildi ({tc_number}). Devam ediliyor...")

        print("[*] PDF oluşturuluyor...")
        pdf_bytes = handler.pdf_generator.generate_vertical_pdf(front_rotated, back_rotated)
        output_path = args.front_path.parent / f"{tc_number}_merged.pdf"
        output_path.write_bytes(pdf_bytes)

        print(f"[✅] Başarılı! PDF kaydedildi: {output_path}")
    except Exception as error:
        raise SystemExit(f"[❌] HATA: {error}")