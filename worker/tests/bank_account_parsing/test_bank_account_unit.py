"""Unit test suite for IbanParser and BankAccountOcrService.

Covers regex formatting variations, deduplication, strict multi-IBAN ambiguity rejection,
boundary false-positive prevention, and Azure Computer Vision Read API v3.2 polling/timeout guards.
"""

from unittest.mock import patch
import pytest

from microservices.bank_account_parsing.config import BankAccountParsingConfig
from microservices.bank_account_parsing.services.iban_parser import IbanParser
from microservices.bank_account_parsing.services.ocr_service import BankAccountOcrService

VALID_IBAN_1 = "TR330006100519786457841326"
VALID_IBAN_2 = "TR730006200001234567890123"


# ============================================================================
# 1. IbanParser Unit Tests
# ============================================================================


@pytest.mark.parametrize(
    ("raw_text", "expected_iban"),
    [
        # Compact 26-character Turkish IBAN
        (f"Vasi Hesap No: {VALID_IBAN_1}", VALID_IBAN_1),
        # Standard 4-character spaced groups
        ("IBAN: TR33 0006 1005 1978 6457 8413 26 Tutar: 2500 TL", VALID_IBAN_1),
        # Hyphen-separated groups and lowercase country prefix
        ("Hesap Bilgisi: tr73-0006-2000-0123-4567-8901-23", VALID_IBAN_2),
        # Space immediately after TR and line break splitting digit groups across two lines
        ("Musteri IBAN: TR 33 0006 1005 1978\n6457 8413 26\nTarih: 27.11.2026", VALID_IBAN_1),
    ],
)
def test_iban_parser_single_valid_candidate_normalizes_and_returns_iban(
    raw_text: str,
    expected_iban: str,
) -> None:
    # Arrange
    parser = IbanParser()

    # Act
    result = parser.extract_single_iban(raw_text)

    # Assert
    assert result == expected_iban


def test_iban_parser_duplicate_identical_iban_deduplicates_to_single_candidate() -> None:
    # Arrange
    parser = IbanParser()
    receipt_text = (
        "Ust Bilgi IBAN: TR33 0006 1005 1978 6457 8413 26\n"
        f"Alt Dekont Onay IBAN: {VALID_IBAN_1}"
    )

    # Act
    candidates = parser.extract_candidates(receipt_text)
    single_iban = parser.extract_single_iban(receipt_text)

    # Assert
    assert candidates == [VALID_IBAN_1]
    assert single_iban == VALID_IBAN_1


def test_iban_parser_two_distinct_ibans_returns_none_per_strict_ambiguity_rule() -> None:
    # Arrange
    parser = IbanParser()
    transfer_receipt = (
        f"Gonderen IBAN: {VALID_IBAN_1}\n"
        "Alici IBAN: TR73 0006 2000 0123 4567 8901 23"
    )

    # Act
    candidates = parser.extract_candidates(transfer_receipt)
    single_iban = parser.extract_single_iban(transfer_receipt)

    # Assert
    assert candidates == [VALID_IBAN_1, VALID_IBAN_2]
    assert single_iban is None, "Birden fazla farkli IBAN bulundugunda belirsizlik nedeniyle None donmelidir"


@pytest.mark.parametrize(
    "non_matching_text",
    [
        "",
        "Bu belgede herhangi bir banka hesap numarasi bulunmamaktadir.",
        # 23 digits after TR (too short)
        "TR33000610051978645784132",
        # 25 digits after TR (too long)
        "TR3300061005197864578413269",
        # Trailing alphanumeric characters attached to 24th digit
        f"{VALID_IBAN_1}ABC",
        # Leading alphanumeric character attached before TR
        f"A{VALID_IBAN_1}",
        # Valid non-Turkish IBAN (German DE prefix)
        "DE89370400440532013000",
    ],
)
def test_iban_parser_zero_or_boundary_invalid_patterns_return_none(
    non_matching_text: str,
) -> None:
    # Arrange
    parser = IbanParser()

    # Act
    candidates = parser.extract_candidates(non_matching_text)
    single_iban = parser.extract_single_iban(non_matching_text)

    # Assert
    assert candidates == []
    assert single_iban is None


# ============================================================================
# 2. BankAccountOcrService HTTP Polling & Error Boundary Tests
# ============================================================================


class _FakeHttpResponse:
    """Minimal async context-manager stub emulating an aiohttp ClientResponse."""

    def __init__(
        self,
        status_code: int = 200,
        headers: dict[str, str] | None = None,
        json_payload: dict | None = None,
    ):
        self.status = status_code
        self.headers = headers or {}
        self._json_payload = json_payload or {}

    async def __aenter__(self) -> "_FakeHttpResponse":
        return self

    async def __aexit__(self, exc_type, exc_val, exc_tb) -> None:
        return None

    def raise_for_status(self) -> None:
        if self.status >= 400:
            raise RuntimeError(f"HTTP {self.status}")

    async def json(self) -> dict:
        return self._json_payload


class _FakeClientSession:
    """Minimal async context-manager stub emulating aiohttp.ClientSession."""

    def __init__(self, post_response: _FakeHttpResponse, get_responses: list[_FakeHttpResponse]):
        self._post_response = post_response
        self._get_responses = list(get_responses)
        self.post_calls: list[tuple[str, dict, bytes]] = []
        self.get_calls: list[tuple[str, dict]] = []

    async def __aenter__(self) -> "_FakeClientSession":
        return self

    async def __aexit__(self, exc_type, exc_val, exc_tb) -> None:
        return None

    def post(self, url: str, headers: dict, data: bytes) -> _FakeHttpResponse:
        self.post_calls.append((url, headers, data))
        return self._post_response

    def get(self, url: str, headers: dict) -> _FakeHttpResponse:
        self.get_calls.append((url, headers))
        if len(self._get_responses) > 1:
            return self._get_responses.pop(0)
        return self._get_responses[0]


def test_ocr_service_polls_running_then_returns_combined_multipage_lines(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    # Arrange
    monkeypatch.setenv("AZURE_VISION_ENDPOINT", "https://fake-vision.cognitiveservices.azure.com/")
    monkeypatch.setenv("AZURE_VISION_KEY", "fake-secret-key")

    config = BankAccountParsingConfig(OCR_POLL_INTERVAL_SECONDS=0.001, OCR_MAX_POLL_ATTEMPTS=5)
    service = BankAccountOcrService(config)

    operation_location = "https://fake-vision.cognitiveservices.azure.com/vision/v3.2/read/analyzeResults/op-123"
    fake_session = _FakeClientSession(
        post_response=_FakeHttpResponse(
            status_code=202,
            headers={"Operation-Location": operation_location},
        ),
        get_responses=[
            _FakeHttpResponse(status_code=200, json_payload={"status": "running"}),
            _FakeHttpResponse(
                status_code=200,
                json_payload={
                    "status": "succeeded",
                    "analyzeResult": {
                        "readResults": [
                            {"page": 1, "lines": [{"text": "Ziraat Bankasi Hesap Ozeti"}]},
                            {"page": 2, "lines": [{"text": f"IBAN: {VALID_IBAN_1}"}]},
                        ]
                    },
                },
            ),
        ],
    )

    # Act
    with patch("aiohttp.ClientSession", return_value=fake_session):
        extracted = service.extract_text(b"raw-document-bytes")

    # Assert
    assert extracted == f"Ziraat Bankasi Hesap Ozeti\nIBAN: {VALID_IBAN_1}"
    assert len(fake_session.post_calls) == 1
    post_url, post_headers, post_data = fake_session.post_calls[0]
    assert post_url == "https://fake-vision.cognitiveservices.azure.com/vision/v3.2/read/analyze"
    assert post_headers["Ocp-Apim-Subscription-Key"] == "fake-secret-key"
    assert post_headers["Content-Type"] == "application/octet-stream"
    assert post_data == b"raw-document-bytes"
    assert len(fake_session.get_calls) == 2


def test_ocr_service_failed_status_raises_value_error(monkeypatch: pytest.MonkeyPatch) -> None:
    # Arrange
    monkeypatch.setenv("AZURE_VISION_ENDPOINT", "https://fake-vision.cognitiveservices.azure.com")
    monkeypatch.setenv("AZURE_VISION_KEY", "fake-secret-key")

    config = BankAccountParsingConfig(OCR_POLL_INTERVAL_SECONDS=0.001, OCR_MAX_POLL_ATTEMPTS=3)
    service = BankAccountOcrService(config)

    fake_session = _FakeClientSession(
        post_response=_FakeHttpResponse(
            status_code=202,
            headers={"Operation-Location": "https://fake-vision/op-fail"},
        ),
        get_responses=[_FakeHttpResponse(status_code=200, json_payload={"status": "failed"})],
    )

    # Act & Assert
    with patch("aiohttp.ClientSession", return_value=fake_session):
        with pytest.raises(ValueError, match="başaramadı"):
            service.extract_text(b"raw-bytes")


def test_ocr_service_exceeding_max_poll_attempts_raises_timeout_error(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    # Arrange
    monkeypatch.setenv("AZURE_VISION_ENDPOINT", "https://fake-vision.cognitiveservices.azure.com")
    monkeypatch.setenv("AZURE_VISION_KEY", "fake-secret-key")

    config = BankAccountParsingConfig(OCR_POLL_INTERVAL_SECONDS=0.001, OCR_MAX_POLL_ATTEMPTS=3)
    service = BankAccountOcrService(config)

    fake_session = _FakeClientSession(
        post_response=_FakeHttpResponse(
            status_code=202,
            headers={"Operation-Location": "https://fake-vision/op-stuck"},
        ),
        get_responses=[_FakeHttpResponse(status_code=200, json_payload={"status": "running"})],
    )

    # Act & Assert
    with patch("aiohttp.ClientSession", return_value=fake_session):
        with pytest.raises(TimeoutError, match="zaman aşımına"):
            service.extract_text(b"raw-bytes")

    assert len(fake_session.get_calls) == 3, "Polling must terminate strictly at OCR_MAX_POLL_ATTEMPTS"


def test_ocr_service_missing_azure_credentials_raises_value_error(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    # Arrange
    monkeypatch.delenv("AZURE_VISION_ENDPOINT", raising=False)
    monkeypatch.delenv("AZURE_VISION_KEY", raising=False)
    service = BankAccountOcrService()

    # Act & Assert
    with pytest.raises(ValueError, match="Azure OCR anahtarları"):
        service.extract_text(b"raw-bytes")
