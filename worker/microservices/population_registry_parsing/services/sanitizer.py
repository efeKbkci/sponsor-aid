import re
from datetime import datetime

class PopulationRegistrySanitizer:
    """Cleans and standardizes raw text fields from population registry tables.

    Converts localized dates and extracts vital status information from cell strings.
    """

    def __init__(self, input_format: str, output_format: str):
        """Initialize the sanitizer with input and output date formats."""
        self.input_format = input_format
        self.output_format = output_format

    def clean_and_format_date(self, date_str: str) -> str:
        """Parse a date string from the input format and convert it to the output format."""
        try:
            parsed_date = datetime.strptime(date_str.strip(), self.input_format)
            return parsed_date.strftime(self.output_format)
        except ValueError as e:
            raise ValueError(f"Kritik Hata: '{date_str}' beklenen tarih formatına uymuyor.") from e

    def parse_birth_date(self, cell_text: str) -> str:
        """Extract the birth date from a combined birthplace and date cell.

        In e-Devlet tables, the birthplace is printed on the upper line and the date is on the last line.
        """
        if not cell_text:
            raise ValueError("Kritik Hata: Doğum tarihi hücresi boş veya okunamadı.")
        
        # Split cell lines to take the final line where the birth date is located.
        parts = cell_text.strip().split('\n')
        date_part = parts[-1].strip()
        return self.clean_and_format_date(date_part)

    def parse_death_date(self, cell_text: str) -> str | None:
        """Extract the death date from the events cell if the individual is deceased.

        Returns None if the person is alive ('SAĞ') or returns the formatted death date.
        """
        if not cell_text:
             raise ValueError("Kritik Hata: Olaylar ve Tarihleri hücresi boş veya okunamadı.")

        match = re.search(r"Ölüm:\s*(.+?)(?:\n|$)", cell_text)
        if not match:
            raise ValueError(f"Kritik Hata: İlgili hücrede 'Ölüm:' etiketi bulunamadı. Metin: {cell_text}")
        
        status_or_date = match.group(1).strip()
        
        # 'SAĞ' indicates the person is currently living, so no death date applies.
        if status_or_date.upper() == "SAĞ":
            return None
            
        return self.clean_and_format_date(status_or_date)
