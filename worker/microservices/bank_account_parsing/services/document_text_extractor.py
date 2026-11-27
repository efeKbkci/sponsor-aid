"""Extracts text from bank account documents using native PDF reading or Azure OCR fallback."""

import io
import os
from pathlib import Path
from PIL import Image
import pdfplumber

from ..config import BankAccountParsingConfig
from .ocr_service import BankAccountOcrService


class DocumentTextExtractor:
    """Implements the 3-case extraction strategy for images, native PDFs, and scanned PDFs."""

    def __init__(
        self,
        config: BankAccountParsingConfig | None = None,
        ocr_service: BankAccountOcrService | None = None,
    ):
        self.config = config or BankAccountParsingConfig()
        self.ocr_service = ocr_service or BankAccountOcrService(self.config)

    def extract_text(self, file_path: str) -> str:
        """Read document from disk and return full extracted text across all pages."""
        if not file_path or not os.path.isfile(file_path):
            raise FileNotFoundError(f"Banka hesap belgesi bulunamadı: {file_path}")

        extension = Path(file_path).suffix.lower()
        supported = self.config.SUPPORTED_IMAGE_EXTENSIONS + self.config.SUPPORTED_PDF_EXTENSIONS
        if extension not in supported:
            raise ValueError(f"Desteklenmeyen dosya uzantısı: '{extension}'.")

        with open(file_path, "rb") as file_handle:
            file_bytes = file_handle.read()

        if not file_bytes:
            raise ValueError("Yüklenen belge dosyası boş.")

        # Case 1: Image file (.jpg, .jpeg, .png) -> validate image integrity then extract via Azure OCR.
        if extension in self.config.SUPPORTED_IMAGE_EXTENSIONS:
            self._validate_image_bytes(file_bytes)
            return self.ocr_service.extract_text(file_bytes)

        # Case 2 & 3: PDF file -> inspect selectable text via pdfplumber across all pages first.
        native_text = self._extract_native_pdf_text(file_bytes)
        non_whitespace_chars = len("".join(native_text.split()))

        if non_whitespace_chars >= self.config.MIN_NATIVE_PDF_TEXT_CHARS:
            return native_text

        # Case 3: Scanned / non-native PDF -> send raw PDF bytes directly to Azure Vision Read API v3.2.
        return self.ocr_service.extract_text(file_bytes)

    @staticmethod
    def _validate_image_bytes(image_bytes: bytes) -> None:
        """Verify that raw bytes represent a readable, non-corrupted image."""
        try:
            with Image.open(io.BytesIO(image_bytes)) as img:
                img.verify()
        except Exception as error:
            raise ValueError(f"Görsel dosyası bozuk veya okunamadı. Detay: {error}") from error

    @staticmethod
    def _extract_native_pdf_text(pdf_bytes: bytes) -> str:
        """Extract selectable text across all pages of a PDF using pdfplumber."""
        try:
            with pdfplumber.open(io.BytesIO(pdf_bytes)) as pdf:
                if not pdf.pages:
                    raise ValueError("PDF dosyasında sayfa bulunamadı.")

                page_texts: list[str] = []
                for page in pdf.pages:
                    text = page.extract_text()
                    if text:
                        page_texts.append(text)

                return "\n".join(page_texts)
        except Exception as error:
            raise ValueError(f"PDF dosyası okunamadı. Detay: {error}") from error
