"""Shared pytest fixtures for generating real native PDFs, scanned PDFs, and images on disk."""

from collections.abc import Callable
from pathlib import Path
import pytest
from PIL import Image


VALID_IBAN_1 = "TR330006100519786457841326"
VALID_IBAN_2 = "TR730006200001234567890123"


def _build_minimal_pdf_14(pages_text: list[str]) -> bytes:
    """Build a valid PDF 1.4 binary with real selectable text streams across one or more pages."""
    if not pages_text:
        pages_text = [""]

    objects: list[bytes] = []
    kids_refs = " ".join(f"{4 + 2 * i} 0 R" for i in range(len(pages_text)))

    # 1: Catalog, 2: Pages root, 3: Standard Type1 Helvetica font
    objects.append(b"<< /Type /Catalog /Pages 2 0 R >>")
    objects.append(f"<< /Type /Pages /Kids [{kids_refs}] /Count {len(pages_text)} >>".encode("ascii"))
    objects.append(b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>")

    for index, page_text in enumerate(pages_text):
        lines = page_text.splitlines() or [""]
        operations = ["BT", "/F1 12 Tf", "50 780 Td"]

        for line_idx, raw_line in enumerate(lines):
            escaped = raw_line.replace("\\", "\\\\").replace("(", "\\(").replace(")", "\\)")
            if line_idx > 0:
                operations.append("0 -18 Td")
            operations.append(f"({escaped}) Tj")

        operations.append("ET")
        stream_bytes = "\n".join(operations).encode("latin-1", errors="replace")

        content_obj_num = 5 + 2 * index
        page_dict = (
            f"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] "
            f"/Resources << /Font << /F1 3 0 R >> >> /Contents {content_obj_num} 0 R >>"
        ).encode("ascii")
        content_stream = (
            f"<< /Length {len(stream_bytes)} >>\nstream\n".encode("ascii")
            + stream_bytes
            + b"\nendstream"
        )

        objects.append(page_dict)
        objects.append(content_stream)

    output = bytearray(b"%PDF-1.4\n")
    offsets: list[int] = [0]

    for obj_number, obj_body in enumerate(objects, start=1):
        offsets.append(len(output))
        output.extend(f"{obj_number} 0 obj\n".encode("ascii"))
        output.extend(obj_body)
        output.extend(b"\nendobj\n")

    xref_offset = len(output)
    output.extend(f"xref\n0 {len(objects) + 1}\n0000000000 65535 f \n".encode("ascii"))
    for offset in offsets[1:]:
        output.extend(f"{offset:010d} 00000 n \n".encode("ascii"))

    output.extend(
        f"trailer\n<< /Size {len(objects) + 1} /Root 1 0 R >>\nstartxref\n{xref_offset}\n%%EOF\n".encode(
            "ascii"
        )
    )
    return bytes(output)


@pytest.fixture
def create_native_pdf(tmp_path: Path) -> Callable[[list[str], str], Path]:
    """Return a factory creating real single- or multi-page native PDFs parsed by pdfplumber."""

    def _factory(pages_text: list[str], filename: str = "native_bank_account.pdf") -> Path:
        target = tmp_path / filename
        target.write_bytes(_build_minimal_pdf_14(pages_text))
        return target

    return _factory


@pytest.fixture
def create_scanned_pdf(tmp_path: Path) -> Callable[[str], Path]:
    """Return a factory creating a real image-only PDF via Pillow with zero selectable text."""

    def _factory(filename: str = "scanned_bank_account.pdf") -> Path:
        target = tmp_path / filename
        image = Image.new("RGB", (600, 300), "white")
        image.save(target, format="PDF")
        return target

    return _factory


@pytest.fixture
def create_image(tmp_path: Path) -> Callable[[str], Path]:
    """Return a factory creating a real valid .jpg or .png image on disk via Pillow."""

    def _factory(ext: str = ".jpg") -> Path:
        normalized_ext = ext if ext.startswith(".") else f".{ext}"
        target = tmp_path / f"bank_receipt{normalized_ext}"
        image = Image.new("RGB", (500, 250), color=(250, 250, 250))
        fmt = "PNG" if normalized_ext.lower() == ".png" else "JPEG"
        image.save(target, format=fmt)
        return target

    return _factory


@pytest.fixture
def corrupted_pdf(tmp_path: Path) -> Path:
    """Create a real .pdf file containing invalid binary garbage."""
    path = tmp_path / "corrupted_document.pdf"
    path.write_bytes(b"NOT_A_VALID_PDF_HEADER_\x00\xff\xfe\xfd_GARBAGE")
    return path


@pytest.fixture
def corrupted_image(tmp_path: Path) -> Path:
    """Create a real .jpg file containing invalid binary garbage."""
    path = tmp_path / "corrupted_receipt.jpg"
    path.write_bytes(b"NOT_A_VALID_JPEG_\x00\xff\xd8\x00_TRUNCATED_GARBAGE")
    return path
