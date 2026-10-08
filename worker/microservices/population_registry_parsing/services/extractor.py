# services/extractor.py
import pdfplumber

class PopulationRegistryExtractor:

    def extract(self, file_path: str) -> list:
        try:
            with pdfplumber.open(file_path) as pdf:
                page = pdf.pages[0]
                
                # 'SIRA' başlığının Y koordinatını tespit et
                sira_y_koordinati = None
                for word in page.extract_words():
                    if word["text"] == "SIRA":
                        sira_y_koordinati = word["top"]
                        break
                        
                if not sira_y_koordinati:
                    raise ValueError("Kritik Hata: Tablo başlığı (SIRA) belgede bulunamadı.")

                # Sayfayı 'SIRA' metninin hemen üzerinden kırp
                bounding_box = (0, sira_y_koordinati - 2, page.width, page.height)
                cropped_page = page.crop(bounding_box)

                # Temizlenmiş bölgeden çizgileri referans alarak tabloyu çıkar
                clean_table = cropped_page.extract_table({
                    "vertical_strategy": "lines",
                    "horizontal_strategy": "lines"
                })
                
                if not clean_table:
                    raise ValueError("Kritik Hata: Kırpılmış PDF içerisinde okunabilir bir tablo bulunamadı.")
                
                return clean_table
                
        except Exception as e:
            raise ValueError(f"Kritik Hata: PDF dosyası okunamadı. Detay: {str(e)}") from e