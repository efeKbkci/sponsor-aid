import pdfplumber

class PopulationRegistryExtractor:
    """Extracts raw table data from official population registry PDF documents.

    Uses word anchors and explicit line geometry to extract the registry table.
    """

    def extract(self, file_path: str) -> list:
        """Extract the family registry table from the first page of the PDF.

        Crops page content above the table header to prevent extraneous header text from corrupting table parsing.
        """
        try:
            with pdfplumber.open(file_path) as pdf:
                page = pdf.pages[0]
                
                # Locate the table header anchor word 'SIRA' to identify where the family registry table begins.
                sira_y_koordinati = None
                for word in page.extract_words():
                    if word["text"] == "SIRA":
                        sira_y_koordinati = word["top"]
                        break
                        
                if not sira_y_koordinati:
                    raise ValueError("Kritik Hata: Tablo başlığı (SIRA) belgede bulunamadı.")

                # Crop above the table header to remove government letterheads and seals that interfere with table parsing.
                bounding_box = (0, sira_y_koordinati - 2, page.width, page.height)
                cropped_page = page.crop(bounding_box)

                # Extract cells using explicit vector lines because standard e-Devlet tables feature well-defined grid borders.
                clean_table = cropped_page.extract_table({
                    "vertical_strategy": "lines",
                    "horizontal_strategy": "lines"
                })
                
                if not clean_table:
                    raise ValueError("Kritik Hata: Kırpılmış PDF içerisinde okunabilir bir tablo bulunamadı.")
                
                return clean_table
                
        except Exception as e:
            raise ValueError(f"Kritik Hata: PDF dosyası okunamadı. Detay: {str(e)}") from e
