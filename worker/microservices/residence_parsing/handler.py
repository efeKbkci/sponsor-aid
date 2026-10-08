import events_pb2
from .config import ResidenceConfig
from .services.extractor import ResidenceExtractor
from .services.sanitizer import ResidenceSanitizer

class ResidenceHandler:
    def __init__(self):
        self.config = ResidenceConfig()
        self.extractor = ResidenceExtractor()
        self.sanitizer = ResidenceSanitizer()

    def process(self, task) -> events_pb2.WorkerResultMessage:
        file_path = task.file_path
        try:
            # 1. PDF'ten Ham Veri Çıkarımı
            raw_data = self.extractor.extract(file_path, self.config)
            
            # 2. Adres Metninin Temizlenmesi ve Parçalanması
            parsed_address = self.sanitizer.parse_address(raw_data["raw_address"])

            print(f"DEBUG | District : {parsed_address["district"]}")

            # 3. Sonucu Protobuf Modeline Çevir
            residence_data = events_pb2.ResidenceData(
                id_number=raw_data["id_number"],
                city=parsed_address["city"],
                district=parsed_address["district"],
                neighborhood=parsed_address["neighborhood"],
                full_address=parsed_address["full_address"]
            )

            return events_pb2.WorkerResultMessage(
                registration_key=task.registration_key,
                document_type=task.document_type,
                is_success=True,
                saved_file_path=file_path,
                residence_data=residence_data
            )

        except Exception as error:
            return events_pb2.WorkerResultMessage(
                registration_key=task.registration_key,
                document_type=task.document_type,
                is_success=False,
                error_message=str(error),
                saved_file_path=file_path,
            )


if __name__ == "__main__":
    import argparse
    from pathlib import Path

    parser = argparse.ArgumentParser(description="İkamet belgesini işler.")
    parser.add_argument("file_path", type=Path, help="İşlenecek PDF dosyasının yolu")
    parser.add_argument("--registration-key", default="local-test")
    args = parser.parse_args()

    if not args.file_path.is_file():
        parser.error(f"PDF dosyası bulunamadı: {args.file_path}")

    task = events_pb2.ProcessOfficialDocTask(
        registration_key=args.registration_key,
        file_path=str(args.file_path),
        document_type=events_pb2.DocumentType.DOCUMENT_TYPE_RESIDENCE,
    )
    result = ResidenceHandler().process(task)
    print(result)
    if not result.is_success:
        raise SystemExit(1)