import re
from datetime import datetime

class PopulationRegistrySanitizer:
    def __init__(self, input_format: str, output_format: str):
        self.input_format = input_format
        self.output_format = output_format

    def clean_and_format_date(self, date_str: str) -> str:
        try:
            parsed_date = datetime.strptime(date_str.strip(), self.input_format)
            return parsed_date.strftime(self.output_format)
        except ValueError as e:
            raise ValueError(f"Kritik Hata: '{date_str}' beklenen tarih formatına uymuyor.") from e

    def parse_birth_date(self, cell_text: str) -> str:
        if not cell_text:
            raise ValueError("Kritik Hata: Doğum tarihi hücresi boş veya okunamadı.")
        
        parts = cell_text.strip().split('\n')
        date_part = parts[-1].strip()
        return self.clean_and_format_date(date_part)

    def parse_death_date(self, cell_text: str) -> str | None:
        if not cell_text:
             raise ValueError("Kritik Hata: Olaylar ve Tarihleri hücresi boş veya okunamadı.")

        match = re.search(r"Ölüm:\s*(.+?)(?:\n|$)", cell_text)
        if not match:
            raise ValueError(f"Kritik Hata: İlgili hücrede 'Ölüm:' etiketi bulunamadı. Metin: {cell_text}")
        
        status_or_date = match.group(1).strip()
        
        if status_or_date.upper() == "SAĞ":
            return None
            
        return self.clean_and_format_date(status_or_date)