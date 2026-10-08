from dataclasses import dataclass

@dataclass(frozen=True)
class PopulationRegistryConfig:
    COL_KINSHIP: int = 3
    COL_ID_NUMBER: int = 4
    COL_FIRST_NAME: int = 5
    COL_LAST_NAME: int = 6
    COL_BIRTH_INFO: int = 9
    COL_EVENTS: int = 12
    
    INPUT_DATE_FORMAT: str = "%d.%m.%Y"
    OUTPUT_DATE_FORMAT: str = "%Y-%m-%d"