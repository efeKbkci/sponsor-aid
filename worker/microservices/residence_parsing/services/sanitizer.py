import re

class ResidenceSanitizer:
    """Parses and sanitizes unstructured Turkish address text into structured geographic entities.

    Extracts city, district, and neighborhood fields using regex pattern heuristics.
    """

    def parse_address(self, raw_address: str) -> dict:
        """Decompose a raw address string into city, district, neighborhood, and full address.

        Uses slash delimiters and standard Turkish address keywords to identify individual parts.
        """
        if not raw_address:
            raise ValueError("Kritik Hata: Çıkarılan adres metni boş.")
        
        # Replace multiple consecutive whitespace characters with a single space.
        full_address = re.sub(r'\s+', ' ', raw_address).strip()
        
        city = ""
        district = ""
        neighborhood = ""
        
        # Standard e-Devlet address format ends with 'DISTRICT / PROVINCE' separated by a forward slash.
        if "/" in full_address:
            left_part, city_part = full_address.rsplit("/", 1)
            city = city_part.strip()
            left_part = left_part.strip()
        else:
            # Fallback for missing slash delimiters: assume the final token represents the province.
            left_part, city_part = full_address.rsplit(" ", 1)
            city = city_part.strip()
            left_part = left_part.strip()

        # In Turkish addresses, street numbers or markers like NO:, MAH., SK., CAD. precede the district name.
        dist_match = re.search(r'.*(?:NO:\s*\S+|KÖYÜ|MAH\.|MEVKİİ|SK\.|CAD\.)\s+(.+)$', left_part, re.IGNORECASE)
        
        if dist_match:
            district = dist_match.group(1).strip()
        else:
            # Fallback to the last word of the remaining prefix if no keyword markers match.
            district = left_part.split()[-1] if left_part else ""

        # Extract neighborhood or village name from the start of the address up to MAH. or KÖYÜ.
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
