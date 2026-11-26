import pdfplumber


class PopulationRegistryExtractor:
    def extract(self, file_path: str) -> list:
        try:
            with pdfplumber.open(file_path) as pdf:
                page = pdf.pages[0]
                words = page.extract_words()

                sira_word = next((w for w in words if w["text"] == "SIRA"), None)
                if not sira_word:
                    raise ValueError("Kritik Hata: Tablo başlığı (SIRA) belgede bulunamadı.")

                # Başlık satırının üst çerçeve çizgisini de dahil et
                upper_edges = [
                    e["top"] for e in page.horizontal_edges
                    if 0 < (sira_word["top"] - e["top"]) <= 30
                ]
                top_y = (max(upper_edges) - 2) if upper_edges else max(0, sira_word["top"] - 20)

                # Alttaki DÜŞÜNCELER tablosunu kırpma alanının dışında bırak
                dusunceler_word = next(
                    (w for w in words if w["text"] == "DÜŞÜNCELER" and w["top"] > sira_word["bottom"]),
                    None
                )
                bottom_y = (dusunceler_word["top"] - 2) if dusunceler_word else page.height

                bounding_box = (0, max(0, top_y), page.width, bottom_y)
                cropped_page = page.crop(bounding_box)

                tables = cropped_page.extract_tables({
                    "vertical_strategy": "lines",
                    "horizontal_strategy": "lines",
                    "snap_tolerance": 4,
                    "join_tolerance": 4,
                })

                if not tables:
                    raise ValueError("Kritik Hata: Kırpılmış PDF içerisinde okunabilir bir tablo bulunamadı.")

                # SIRA başlığını içeren ana tabloyu seç, bulunamazsa ilk tabloyu dön
                for table in tables:
                    if any(cell and "SIRA" in str(cell) for row in table[:2] for cell in row):
                        return table

                return tables[0]
        except Exception as e:
            raise ValueError(f"Kritik Hata: PDF dosyası okunamadı. Detay: {str(e)}") from e