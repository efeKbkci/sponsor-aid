import events_pb2
import numpy as np
import cv2
from .services.scanner import BarcodeScannerService

class BarcodeExtractionHandler:
    def __init__(self):
        self.scanner = BarcodeScannerService()

    def process(self, task) -> events_pb2.WorkerResultMessage:
        file_path = task.file_path

        with open(file_path, "rb") as f:
            image_bytes = f.read()

            np_array = np.frombuffer(image_bytes, np.uint8)
            image = cv2.imdecode(np_array, cv2.IMREAD_COLOR)
            
            if image is None:
                raise ValueError("Kritik Hata: Fotoğraf okunamadı veya format desteklenmiyor.")
        
        try:
            extracted_barcode = self.scanner.extract(image)

            return events_pb2.WorkerResultMessage(
                registration_key=task.registration_key,
                document_type=task.document_type, 
                is_success=True,
                saved_file_path=file_path,
                extracted_barcode=extracted_barcode # C#'ın beklediği kritik şifre
            )

        except Exception as error:
            return events_pb2.WorkerResultMessage(
                registration_key=task.registration_key,
                document_type=events_pb2.DocumentType.DOCUMENT_TYPE_UNKNOWN,
                is_success=False,
                error_message=str(error),
                saved_file_path=file_path
            )


if __name__ == "__main__":
    import argparse
    from pathlib import Path

    parser = argparse.ArgumentParser(description="Görüntüdeki doğrulama barkodunu okur.")
    parser.add_argument("file_path", type=Path, help="Taranacak görüntünün yolu")
    parser.add_argument("--registration-key", default="local-test")
    args = parser.parse_args()

    if not args.file_path.is_file():
        parser.error(f"Görüntü dosyası bulunamadı: {args.file_path}")

    task = events_pb2.ProcessOfficialDocTask(
        registration_key=args.registration_key,
        file_path=str(args.file_path),
        document_type=events_pb2.DocumentType.DOCUMENT_TYPE_UNKNOWN,
    )

    result = BarcodeExtractionHandler().process(task)
    print(result)
    if not result.is_success:
        raise SystemExit(1)