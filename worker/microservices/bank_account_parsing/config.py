"""Configuration parameters for bank account document parsing and IBAN extraction."""

from dataclasses import dataclass


@dataclass(frozen=True)
class BankAccountParsingConfig:
    """Thresholds, supported file extensions, and Azure OCR polling limits."""

    # Minimum non-whitespace character count across all PDF pages to treat a PDF as native digital text.
    MIN_NATIVE_PDF_TEXT_CHARS: int = 20

    SUPPORTED_IMAGE_EXTENSIONS: tuple[str, ...] = (".jpg", ".jpeg", ".png")
    SUPPORTED_PDF_EXTENSIONS: tuple[str, ...] = (".pdf",)

    AZURE_VISION_ENDPOINT_ENV: str = "AZURE_VISION_ENDPOINT"
    AZURE_VISION_KEY_ENV: str = "AZURE_VISION_KEY"

    OCR_POLL_INTERVAL_SECONDS: float = 0.5
    OCR_MAX_POLL_ATTEMPTS: int = 60
    OCR_HTTP_TIMEOUT_SECONDS: float = 30.0
