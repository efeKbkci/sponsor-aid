import io
from PIL import Image

class PdfGenerator:
    # Arayüz bağımlılığı silindi, metot ve değişkenler snake_case yapıldı.
    def generate_vertical_pdf(self, front_image: bytes, back_image: bytes) -> bytes:
        # 1. Byte dizilerini Pillow Image nesnelerine dönüştür
        front_img = Image.open(io.BytesIO(front_image))
        back_img = Image.open(io.BytesIO(back_image))

        # 3. Standart Kimlik Boyutu Belirleme (Örn: 800x500 piksel)
        target_width = 800
        target_height = 500
        
        front_img_resized = front_img.resize((target_width, target_height), Image.Resampling.LANCZOS)
        back_img_resized = back_img.resize((target_width, target_height), Image.Resampling.LANCZOS)

        # 4. Standart A4 Oranlarına Yakın Bir Tuval (Canvas) Hazırla
        canvas_width = 1000
        canvas_height = 1400 
        
        pdf_canvas = Image.new("RGB", (canvas_width, canvas_height), color=(255, 255, 255))

        # 5. Kimlikleri Tuvale Ortala ve Yapıştır
        center_x = (canvas_width - target_width) // 2
        
        front_y = 150
        pdf_canvas.paste(front_img_resized, (center_x, front_y))

        back_y = front_y + target_height + 100
        pdf_canvas.paste(back_img_resized, (center_x, back_y))

        # 6. PDF Olarak Hafızaya Yaz ve Çıktı Ver
        pdf_buffer = io.BytesIO()
        pdf_canvas.save(pdf_buffer, format="PDF", resolution=100.0, quality=95)
        
        return pdf_buffer.getvalue()