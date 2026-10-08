import pdfplumber
import re

class ResidenceExtractor:
    def extract(self, file_path: str, config) -> dict:
        try:
            with pdfplumber.open(file_path) as pdf:
                page = pdf.pages[0]
                words = page.extract_words()
                
                id_number = None
                adres_no_word = None
                
                # 1. Çıpaları (Anchors) Bulma
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

                # ==========================================
                # 2. UZAMSAL KAZIMA (GÜNCELLENEN BÖLÜM)
                # ==========================================
                address_words = []
                
                # Sol ve Üst sınırlar (Adres No'nun hemen sağı ve üst hizası)
                x_anchor = adres_no_word['x1'] + 2 
                y_anchor_top = adres_no_word['top'] - 5 
                
                # ALT SINIR STRATEJİSİ
                y_anchor_bottom = page.height
                
                # Adım 1: Adres No'nun altından geçen İLK yatay çizgiyi (tablo alt sınırı) bul
                for edge in page.horizontal_edges:
                    if edge['top'] > adres_no_word['bottom']:
                        if edge['top'] < y_anchor_bottom:
                            y_anchor_bottom = edge['top']
                            
                # Adım 2 (Güvenlik Sigortası): Çizgi çok aşağıdaysa veya yoksa, adres maksimum 4-5 satır 
                # (~45 piksel) olabileceği için sert bir matematiksel alt sınır koy.
                max_allowed_bottom = adres_no_word['bottom'] + 45
                if y_anchor_bottom > max_allowed_bottom:
                    y_anchor_bottom = max_allowed_bottom

                # Sadece bu kesin çerçevenin içinde "başlayan" kelimeleri topla
                for w in words:
                    # w['bottom'] yerine w['top'] < y_anchor_bottom kullanıyoruz çünkü
                    # 'g, y, p' gibi harflerin kuyrukları çizginin altına hafifçe taşabilir.
                    if w['x0'] > x_anchor and w['top'] >= y_anchor_top and w['top'] < y_anchor_bottom:
                        address_words.append(w)

                if not address_words:
                    raise ValueError("Kritik Hata: Adres alanı boş.")

                # 3. Kelimeleri Görsel Hizalarına Göre (Satır Satır) Birleştir
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