import pdfplumber
import re

class ResidenceExtractor:
    """Extracts citizen ID and address blocks from residence certificates using spatial coordinates.

    Uses geometric anchors and table edge coordinates to isolate multi-line address text.
    """

    def extract(self, file_path: str, config) -> dict:
        """Locate anchors and extract the full address block from the first page of the PDF.

        Finds 11-digit citizen ID and 10-digit address code, calculates bounding limits, and reconstructs lines.
        """
        try:
            with pdfplumber.open(file_path) as pdf:
                page = pdf.pages[0]
                words = page.extract_words()
                
                id_number = None
                adres_no_word = None
                
                # Locate the 11-digit citizen ID and 10-digit UAVT address code as spatial anchors.
                for word in words:
                    text = word['text']
                    if not id_number and re.fullmatch(r"\d{11}", text):
                        id_number = text
                    if not adres_no_word and re.fullmatch(r"\d{10}", text):
                        adres_no_word = word
                        
                if not id_number:
                    raise ValueError("Kritik Hata: Belgede 11 haneli Kimlik No bulunamadı.")
                if not adres_no_word:
                    raise ValueError("Kritik Hata: Belgede 10 haneli Adres No (UAVT) bulunamadı.")

                # Define spatial scraping bounds relative to the address code anchor.
                address_words = []
                
                # Full address is printed to the right of the address code. An offset of -5px ensures tall ascenders are included.
                x_anchor = adres_no_word['x1'] + 2 
                y_anchor_top = adres_no_word['top'] - 5 
                
                # Bottom boundary strategy: locate the first table divider line below the anchor.
                y_anchor_bottom = page.height
                
                # Identify the first horizontal line below the address code anchor to define the cell border.
                for edge in page.horizontal_edges:
                    if edge['top'] > adres_no_word['bottom']:
                        if edge['top'] < y_anchor_bottom:
                            y_anchor_bottom = edge['top']
                            
                # Fallback limit of 45 pixels ensures footer text is not captured if the table line is missing.
                max_allowed_bottom = adres_no_word['bottom'] + 45
                if y_anchor_bottom > max_allowed_bottom:
                    y_anchor_bottom = max_allowed_bottom

                # Collect words inside the boundary. Compare top coordinate because character descenders can cross cell lines.
                for w in words:
                    if w['x0'] > x_anchor and w['top'] >= y_anchor_top and w['top'] < y_anchor_bottom:
                        address_words.append(w)

                if not address_words:
                    raise ValueError("Kritik Hata: Adres alanı boş.")

                # Quantize Y coordinates into 4-pixel buckets to group words on the same line, then sort horizontally.
                address_words.sort(key=lambda w: (round(w['top'] / 4), w['x0']))
                
                raw_address = ""
                current_y_group = round(address_words[0]['top'] / 4)
                
                for w in address_words:
                    word_y_group = round(w['top'] / 4)
                    
                    if word_y_group != current_y_group:
                        raw_address += "\n"
                        current_y_group = word_y_group
                    else:
                        if raw_address and not raw_address.endswith("\n"):
                            raw_address += " "
                    raw_address += w['text']

                return {
                    "id_number": id_number,
                    "raw_address": raw_address.strip()
                }

        except Exception as e:
            raise ValueError(f"Kritik Hata: Yerleşim yeri belgesi okunamadı. Detay: {str(e)}") from e
