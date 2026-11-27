"""Handler for bank account document processing tasks."""

import events_pb2
from .config import BankAccountParsingConfig
from .services.document_text_extractor import DocumentTextExtractor
from .services.iban_parser import IbanParser
from .services.ocr_service import BankAccountOcrService


class BankAccountParsingHandler:
    """Coordinates document text extraction, IBAN candidate parsing, and Protobuf result assembly."""

    def __init__(
        self,
        config: BankAccountParsingConfig | None = None,
        extractor: DocumentTextExtractor | None = None,
        iban_parser: IbanParser | None = None,
    ):
        self.config = config or BankAccountParsingConfig()
        ocr_service = BankAccountOcrService(self.config)
        self.extractor = extractor or DocumentTextExtractor(self.config, ocr_service)
        self.iban_parser = iban_parser or IbanParser()

    def process(self, task) -> events_pb2.WorkerResultMessage:
        """Process a bank account document task and return a WorkerResultMessage.

        Returns is_success=True whenever the file is readable and processed cleanly,
        populating extracted_iban only when exactly one unique Turkish IBAN candidate is found.
        Returns is_success=False only on technical failures (corrupted file, unsupported format, OCR failure).
        """
        file_path = task.file_path
        try:
            extracted_text = self.extractor.extract_text(file_path)
            extracted_iban = self.iban_parser.extract_single_iban(extracted_text)

            result_kwargs = {
                "registration_key": task.registration_key,
                "document_type": events_pb2.DocumentType.DOCUMENT_TYPE_BANK_ACCOUNT,
                "is_success": True,
                "saved_file_path": file_path,
            }
            if extracted_iban is not None:
                result_kwargs["extracted_iban"] = extracted_iban

            return events_pb2.WorkerResultMessage(**result_kwargs)
        except Exception as error:
            return events_pb2.WorkerResultMessage(
                registration_key=task.registration_key,
                document_type=events_pb2.DocumentType.DOCUMENT_TYPE_BANK_ACCOUNT,
                is_success=False,
                error_message=str(error),
                saved_file_path=file_path,
            )


if __name__ == "__main__":
    import argparse
    from pathlib import Path
    from dotenv import load_dotenv, find_dotenv

    load_dotenv(find_dotenv())

    parser = argparse.ArgumentParser(description="Banka hesap belgesini işler ve IBAN bilgisini çıkarır.")
    parser.add_argument("file_path", type=Path, help="İşlenecek PDF veya görsel dosyasının yolu")
    parser.add_argument("--registration-key", default="local-test")
    args = parser.parse_args()

    if not args.file_path.is_file():
        parser.error(f"Belge dosyası bulunamadı: {args.file_path}")

    sample_task = events_pb2.ProcessBankAccountDocTask(
        registration_key=args.registration_key,
        file_path=str(args.file_path),
    )
    result = BankAccountParsingHandler().process(sample_task)
    print(result)
    if not result.is_success:
        raise SystemExit(1)
