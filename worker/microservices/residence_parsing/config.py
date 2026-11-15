from dataclasses import dataclass

@dataclass(frozen=True)
class ResidenceConfig:
    """Configuration identifiers and regex patterns for residence certificate parsing.

    Stores header tokens and regex rules to locate citizen ID and address blocks in residence PDFs.
    """
    ADDRESS_HEADER_IDENTIFIER: str = "Adres Tipi"
    ID_NUMBER_REGEX: str = r"Kimlik No\s*[:\s]+(\d{11})"
