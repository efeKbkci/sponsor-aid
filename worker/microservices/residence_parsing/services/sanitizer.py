import re

class ResidenceSanitizer:
    def parse_address(self, raw_address: str) -> dict:
        if not raw_address:
            raise ValueError("Kritik Hata: Çıkarılan adres metni boş.")
        
        # 1. Satır atlamalarını düzleştir
        full_address = re.sub(r'\s+', ' ', raw_address).strip()
        
        city = ""
        district = ""
        neighborhood = ""
        
        # 2. İl ve İlçe Ayrıştırması
        if "/" in full_address:
            left_part, city_part = full_address.rsplit("/", 1)
            city = city_part.strip()
            left_part = left_part.strip()
        else:
            # HATA YAKALAMA (FALLBACK): Eğer kütüphane '/' işaretini yutmuşsa 
            # Son kelimeyi İl, geri kalan sol metni ilçe tespiti için kullan.
            left_part, city_part = full_address.rsplit(" ", 1)
            city = city_part.strip()
            left_part = left_part.strip()

        # İlçe Tespiti: Sol kısımdaki EN SON adres belirteçlerinden sonraki metin ilçedir
        dist_match = re.search(r'.*(?:NO:\s*\S+|KÖYÜ|MAH\.|MEVKİİ|SK\.|CAD\.)\s+(.+)$', left_part, re.IGNORECASE)
        
        if dist_match:
            district = dist_match.group(1).strip()
        else:
            # Belirteç yoksa sol kısmın son kelimesini ilçe kabul et
            district = left_part.split()[-1] if left_part else ""

        # 3. Mahalle / Köy Ayrıştırması
        mah_match = re.search(r"^(.*? MAH\.)", full_address, re.IGNORECASE)
        if mah_match:
            neighborhood = mah_match.group(1).strip()
        else:
            koyu_match = re.search(r"^(.*? KÖYÜ)", full_address, re.IGNORECASE)
            if koyu_match:
                neighborhood = koyu_match.group(1).strip()

        return {
            "city": city,
            "district": district,
            "neighborhood": neighborhood,
            "full_address": full_address
        }