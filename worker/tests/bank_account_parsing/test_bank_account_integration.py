"""Integration test suite for BankAccountParsingHandler with real PDF and image files.

Executes real pdfplumber multi-page parsing, real Pillow image verification, and real
Protobuf binary serialization while mocking only the external Azure Vision OCR boundary.
"""

from collections.abc import Callable
from pathlib import Path
from unittest.mock import MagicMock
import pytest

import events_pb2
from microservices.bank_account_parsing.handler import BankAccountParsingHandler

VALID_IBAN_1 = "TR330006100519786457841326"
VALID_IBAN_2 = "TR730006200001234567890123"


def _build_handler_with_ocr_mock() -> tuple[BankAccountParsingHandler, MagicMock]:
    """Create a real BankAccountParsingHandler with only the external OCR call mocked."""
    handler = BankAccountParsingHandler()
    ocr_mock = MagicMock(name="extract_text")
    handler.extractor.ocr_service.extract_text = ocr_mock
    return handler, ocr_mock


def test_native_pdf_with_single_iban_extracts_directly_without_calling_ocr(
    create_native_pdf: Callable[[list[str], str], Path],
) -> None:
    # Arrange
    pdf_path = create_native_pdf(
        [
            "T.C. Ziraat Bankasi Hesap Cuzdani Ozeti\n"
            "Musteri: Ayse Yilmaz\n"
            "IBAN: TR33 0006 1005 1978 6457 8413 26"
        ],
        "single_iban.pdf",
    )
    handler, ocr_spy = _build_handler_with_ocr_mock()
    task = events_pb2.ProcessBankAccountDocTask(
        registration_key="REG001",
        file_path=str(pdf_path),
    )

    # Act
    result = handler.process(task)

    # Assert
    assert result.is_success is True
    assert result.document_type == events_pb2.DocumentType.DOCUMENT_TYPE_BANK_ACCOUNT
    assert result.saved_file_path == str(pdf_path)
    assert result.WhichOneof("extracted_data") == "extracted_iban"
    assert result.extracted_iban == VALID_IBAN_1
    ocr_spy.assert_not_called()


def test_native_multipage_pdf_with_iban_on_page_two_extracts_without_calling_ocr(
    create_native_pdf: Callable[[list[str], str], Path],
) -> None:
    # Arrange
    pdf_path = create_native_pdf(
        [
            "Sayfa 1 - Genel Bilgilendirme ve Hesap Sozlesmesi Ozeti",
            "Sayfa 2 - Hesap Detaylari\nIBAN No: TR73 0006 2000 0123 4567 8901 23",
        ],
        "multipage_iban.pdf",
    )
    handler, ocr_spy = _build_handler_with_ocr_mock()
    task = events_pb2.ProcessBankAccountDocTask(
        registration_key="REG002",
        file_path=str(pdf_path),
    )

    # Act
    result = handler.process(task)

    # Assert
    assert result.is_success is True
    assert result.extracted_iban == VALID_IBAN_2
    ocr_spy.assert_not_called()


def test_native_pdf_with_two_distinct_ibans_returns_success_with_extracted_iban_unset(
    create_native_pdf: Callable[[list[str], str], Path],
) -> None:
    # Arrange
    pdf_path = create_native_pdf(
        [
            f"Gonderen Hesap IBAN: {VALID_IBAN_1}\n"
            f"Alici Hesap IBAN: {VALID_IBAN_2}\n"
            "Aciklama: Kira Odemesi Dekontu"
        ],
        "ambiguous_two_ibans.pdf",
    )
    handler, ocr_spy = _build_handler_with_ocr_mock()
    task = events_pb2.ProcessBankAccountDocTask(
        registration_key="REG003",
        file_path=str(pdf_path),
    )

    # Act
    result = handler.process(task)

    # Assert
    assert result.is_success is True
    assert result.document_type == events_pb2.DocumentType.DOCUMENT_TYPE_BANK_ACCOUNT
    assert result.saved_file_path == str(pdf_path)
    assert result.WhichOneof("extracted_data") is None
    ocr_spy.assert_not_called()


def test_native_pdf_with_zero_ibans_returns_success_with_extracted_iban_unset(
    create_native_pdf: Callable[[list[str], str], Path],
) -> None:
    # Arrange
    pdf_path = create_native_pdf(
        ["Banka HesapAcilis Bilgilendirme Formu - Bu sayfada IBAN bilgisi yer almamaktadir."],
        "no_iban.pdf",
    )
    handler, ocr_spy = _build_handler_with_ocr_mock()
    task = events_pb2.ProcessBankAccountDocTask(
        registration_key="REG004",
        file_path=str(pdf_path),
    )

    # Act
    result = handler.process(task)

    # Assert
    assert result.is_success is True
    assert result.WhichOneof("extracted_data") is None
    ocr_spy.assert_not_called()


def test_scanned_image_only_pdf_falls_back_to_ocr_with_pdf_bytes(
    create_scanned_pdf: Callable[[str], Path],
) -> None:
    # Arrange
    scanned_pdf = create_scanned_pdf("scanned_receipt.pdf")
    expected_pdf_bytes = scanned_pdf.read_bytes()

    handler, ocr_spy = _build_handler_with_ocr_mock()
    ocr_spy.return_value = "Taranmis Dekont\nTR33 0006 1005 1978 6457 8413 26"

    task = events_pb2.ProcessBankAccountDocTask(
        registration_key="REG005",
        file_path=str(scanned_pdf),
    )

    # Act
    result = handler.process(task)

    # Assert
    assert result.is_success is True
    assert result.extracted_iban == VALID_IBAN_1
    ocr_spy.assert_called_once_with(expected_pdf_bytes)


@pytest.mark.parametrize("image_ext", [".jpg", ".png"])
def test_valid_images_verify_with_pillow_and_extract_via_ocr(
    create_image: Callable[[str], Path],
    image_ext: str,
) -> None:
    # Arrange
    img_path = create_image(image_ext)
    expected_bytes = img_path.read_bytes()

    handler, ocr_spy = _build_handler_with_ocr_mock()
    ocr_spy.return_value = f"Vasi Banka Karti\n{VALID_IBAN_2}"

    task = events_pb2.ProcessBankAccountDocTask(
        registration_key="REG006",
        file_path=str(img_path),
    )

    # Act
    result = handler.process(task)

    # Assert
    assert result.is_success is True
    assert result.saved_file_path == str(img_path)
    assert result.extracted_iban == VALID_IBAN_2
    ocr_spy.assert_called_once_with(expected_bytes)


def test_valid_image_with_ambiguous_two_ibans_in_ocr_returns_success_without_iban(
    create_image: Callable[[str], Path],
) -> None:
    # Arrange
    img_path = create_image(".jpg")
    handler, ocr_spy = _build_handler_with_ocr_mock()
    ocr_spy.return_value = f"IBAN 1: {VALID_IBAN_1}\nIBAN 2: {VALID_IBAN_2}"

    task = events_pb2.ProcessBankAccountDocTask(
        registration_key="REG007",
        file_path=str(img_path),
    )

    # Act
    result = handler.process(task)

    # Assert
    assert result.is_success is True
    assert result.WhichOneof("extracted_data") is None
    ocr_spy.assert_called_once()


def test_corrupted_image_fails_before_calling_ocr(corrupted_image: Path) -> None:
    # Arrange
    handler, ocr_spy = _build_handler_with_ocr_mock()
    task = events_pb2.ProcessBankAccountDocTask(
        registration_key="ERR001",
        file_path=str(corrupted_image),
    )

    # Act
    result = handler.process(task)

    # Assert
    assert result.is_success is False
    assert result.document_type == events_pb2.DocumentType.DOCUMENT_TYPE_BANK_ACCOUNT
    assert result.HasField("error_message") and len(result.error_message) > 0
    ocr_spy.assert_not_called()


def test_corrupted_pdf_returns_technical_failure(corrupted_pdf: Path) -> None:
    # Arrange
    handler, ocr_spy = _build_handler_with_ocr_mock()
    task = events_pb2.ProcessBankAccountDocTask(
        registration_key="ERR002",
        file_path=str(corrupted_pdf),
    )

    # Act
    result = handler.process(task)

    # Assert
    assert result.is_success is False
    assert result.HasField("error_message") and len(result.error_message) > 0
    ocr_spy.assert_not_called()


def test_nonexistent_file_and_unsupported_extension_return_technical_failures(
    tmp_path: Path,
) -> None:
    # Arrange
    handler, ocr_spy = _build_handler_with_ocr_mock()
    missing_path = tmp_path / "does_not_exist.pdf"
    unsupported_txt = tmp_path / "iban_note.txt"
    unsupported_txt.write_text(VALID_IBAN_1, encoding="utf-8")

    # Act
    res_missing = handler.process(
        events_pb2.ProcessBankAccountDocTask(registration_key="ERR003", file_path=str(missing_path))
    )
    res_unsupported = handler.process(
        events_pb2.ProcessBankAccountDocTask(registration_key="ERR004", file_path=str(unsupported_txt))
    )

    # Assert
    assert res_missing.is_success is False
    assert res_missing.HasField("error_message") and len(res_missing.error_message) > 0

    assert res_unsupported.is_success is False
    assert res_unsupported.HasField("error_message") and len(res_unsupported.error_message) > 0
    ocr_spy.assert_not_called()


@pytest.mark.parametrize(
    "ocr_exception",
    [
        TimeoutError("Azure OCR işlemi zaman aşımına uğradı."),
        ValueError("Azure OCR okuma işlemini başaramadı."),
    ],
)
def test_ocr_exceptions_surface_as_technical_failure_result(
    create_image: Callable[[str], Path],
    ocr_exception: Exception,
) -> None:
    # Arrange
    img_path = create_image(".png")
    handler, ocr_spy = _build_handler_with_ocr_mock()
    ocr_spy.side_effect = ocr_exception

    task = events_pb2.ProcessBankAccountDocTask(
        registration_key="ERR005",
        file_path=str(img_path),
    )

    # Act
    result = handler.process(task)

    # Assert
    assert result.is_success is False
    assert result.document_type == events_pb2.DocumentType.DOCUMENT_TYPE_BANK_ACCOUNT
    assert str(ocr_exception) in result.error_message


def test_protobuf_wire_contract_roundtrip_preserves_envelope_and_result_fields(
    create_native_pdf: Callable[[list[str], str], Path],
) -> None:
    # Arrange
    pdf_path = create_native_pdf(
        [f"Hesap Sahibinin IBAN Bilgisi:\n{VALID_IBAN_1}"],
        "wire_roundtrip.pdf",
    )
    outbound_envelope = events_pb2.WorkerTaskEnvelope(
        command_type=events_pb2.CommandType.COMMAND_TYPE_PROCESS_BANK_ACCOUNT_DOC,
        bank_account_doc_task=events_pb2.ProcessBankAccountDocTask(
            registration_key="WIRE01",
            file_path=str(pdf_path),
        ),
    )

    # Act: Binary wire serialize -> deserialize -> execute handler -> binary wire serialize -> deserialize
    raw_task_bytes = outbound_envelope.SerializeToString()
    inbound_envelope = events_pb2.WorkerTaskEnvelope()
    inbound_envelope.ParseFromString(raw_task_bytes)

    assert inbound_envelope.command_type == events_pb2.CommandType.COMMAND_TYPE_PROCESS_BANK_ACCOUNT_DOC
    assert inbound_envelope.WhichOneof("task_payload") == "bank_account_doc_task"

    handler, ocr_spy = _build_handler_with_ocr_mock()
    worker_result = handler.process(inbound_envelope.bank_account_doc_task)

    raw_result_bytes = worker_result.SerializeToString()
    decoded_result = events_pb2.WorkerResultMessage()
    decoded_result.ParseFromString(raw_result_bytes)

    # Assert
    assert decoded_result.registration_key == "WIRE01"
    assert decoded_result.document_type == events_pb2.DocumentType.DOCUMENT_TYPE_BANK_ACCOUNT
    assert decoded_result.is_success is True
    assert decoded_result.saved_file_path == str(pdf_path)
    assert decoded_result.WhichOneof("extracted_data") == "extracted_iban"
    assert decoded_result.extracted_iban == VALID_IBAN_1
    ocr_spy.assert_not_called()
