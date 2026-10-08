from dataclasses import dataclass

@dataclass(frozen=True)
class ResidenceConfig:
    ADDRESS_HEADER_IDENTIFIER: str = "Adres Tipi"
    ID_NUMBER_REGEX: str = r"Kimlik No\s*[:\s]+(\d{11})"