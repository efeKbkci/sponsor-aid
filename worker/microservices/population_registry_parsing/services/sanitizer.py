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

        date_match = re.search(r"\b\d{1,2}\.\d{2}\.\d{4}\b", cell_text)
        if date_match:
            return self.clean_and_format_date(date_match.group(0))

        parts = cell_text.strip().split("\n")
        return self.clean_and_format_date(parts[-1].strip())

    def parse_death_date(self, cell_text: str) -> str | None:
        if not cell_text:
            raise ValueError("Kritik Hata: Olaylar ve Tarihleri hücresi boş veya okunamadı.")

        if "Ölüm:" not in cell_text:
            raise ValueError(f"Kritik Hata: İlgili hücrede 'Ölüm:' etiketi bulunamadı. Metin: {cell_text}")

        # e-Devlet PDF'inde 'SAĞ' etiketi 'Ölüm:' satırının üstünde veya altında çıkabilir
        if re.search(r"\bSA[ĞG]\b", cell_text, re.IGNORECASE):
            return None

        # Önce 'Ölüm:' etiketini doğrudan takip eden tarihi ara
        direct_match = re.search(r"Ölüm:\s*(\d{1,2}\.\d{2}\.\d{4})", cell_text)
        if direct_match:
            return self.clean_and_format_date(direct_match.group(1))

        # Etiketler ve tarihler blok halinde ayrışmışsa (Ölüm/Evlenme/Boşanma ardından tarihler),
        # 'SAĞ' olmadığı için ilk tarih ölüm tarihidir
        any_date = re.search(r"\b(\d{1,2}\.\d{2}\.\d{4})\b", cell_text)
        if any_date:
            return self.clean_and_format_date(any_date.group(1))

        raise ValueError(f"Kritik Hata: Ölüm durumu/tarihi ayrıştırılamadı. Metin: {cell_text}")