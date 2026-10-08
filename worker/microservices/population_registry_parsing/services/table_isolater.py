import pdfplumber

def ana_tabloyu_izole_et(pdf_path: str):
    with pdfplumber.open(pdf_path) as pdf:
        page = pdf.pages[0]
        
        # 1. Sayfadaki kelimeleri tara ve 'SIRA' başlığının dikey (Y) koordinatını bul
        sira_y_koordinati = None
        for word in page.extract_words():
            if word["text"] == "SIRA":
                sira_y_koordinati = word["top"]
                break
                
        if not sira_y_koordinati:
            raise ValueError("Tablo başlığı (SIRA) belgede bulunamadı.")

        # 2. Sayfayı 'SIRA' metninin hemen üzerinden (örneğin 2 piksel üstünden) kes
        # Kırpma kutusu: (Sol X, Üst Y, Sağ X, Alt Y)
        bounding_box = (0, sira_y_koordinati - 2, page.width, page.height)
        cropped_page = page.crop(bounding_box)

        # 3. Kırpılmış, tertemiz sayfadan sadece çizgileri referans alarak tabloyu çıkar
        clean_table = cropped_page.extract_table({
            "vertical_strategy": "lines",
            "horizontal_strategy": "lines"
        })
        
        return clean_table

# Test edelim
if __name__ == "__main__":
    tablo = ana_tabloyu_izole_et(r"C:\Users\efkan\Desktop\yetim\bahar_celikbilek\nufus.pdf")
    
    # Sadece ilk 2 satırı (Başlık ve ilk kişi) yazdırarak temizlendiğini görelim
    for row in tablo:
        print(row)