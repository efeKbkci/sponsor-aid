import events_pb2
from .config import ResidenceConfig
from .services.extractor import ResidenceExtractor
from .services.sanitizer import ResidenceSanitizer

class ResidenceHandler:
    """Handles parsing and extraction tasks for residence certificate documents.

    Extracts citizen ID and address blocks, parses address components, and outputs protobuf messages.
    """

    def __init__(self):
        self.config = ResidenceConfig()
        self.extractor = ResidenceExtractor()
        self.sanitizer = ResidenceSanitizer()

    def process(self, task) -> events_pb2.WorkerResultMessage:
        """Process the residence certificate PDF and return structured address information.

        Coordinates spatial word extraction, address component decomposition, and error reporting.
        """
        file_path = task.file_path
        try:
            # Extract raw citizen ID and address text via spatial coordinates.
            raw_data = self.extractor.extract(file_path, self.config)
            
            # Parse raw address string into city, district, and neighborhood fields.
            parsed_address = self.sanitizer.parse_address(raw_data["raw_address"])

            print(f"DEBUG | District : {parsed_address["district"]}")

            # Map the parsed fields into the structured protobuf payload.
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
