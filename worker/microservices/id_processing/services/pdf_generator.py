import io
from PIL import Image

class PdfGenerator:
    """Generates standardized PDF documents containing both sides of an ID card.

    Arranges front and back identity images vertically on a standard A4 canvas.
    """

    def generate_vertical_pdf(self, front_image: bytes, back_image: bytes) -> bytes:
        """Compose front and back ID images into a single vertical PDF file.

        Resizes both images to uniform dimensions and centers them on an A4-proportioned canvas.
        """
        # Convert raw byte arrays into Pillow image instances for manipulation.
        front_img = Image.open(io.BytesIO(front_image))
        back_img = Image.open(io.BytesIO(back_image))

        # Resize cards to standard 800x500 dimensions to maintain the physical card aspect ratio.
        target_width = 800
        target_height = 500
        
        front_img_resized = front_img.resize((target_width, target_height), Image.Resampling.LANCZOS)
        back_img_resized = back_img.resize((target_width, target_height), Image.Resampling.LANCZOS)

        # 1000x1400 canvas approximates vertical A4 paper proportions for document archiving.
        canvas_width = 1000
        canvas_height = 1400 
        
        pdf_canvas = Image.new("RGB", (canvas_width, canvas_height), color=(255, 255, 255))

        # Center cards horizontally and stack them vertically with consistent spacing.
        center_x = (canvas_width - target_width) // 2
        
        front_y = 150
        pdf_canvas.paste(front_img_resized, (center_x, front_y))

        back_y = front_y + target_height + 100
        pdf_canvas.paste(back_img_resized, (center_x, back_y))

        # Save canvas to an in-memory byte buffer to avoid intermediate disk writes.
        pdf_buffer = io.BytesIO()
        pdf_canvas.save(pdf_buffer, format="PDF", resolution=100.0, quality=95)
        
        return pdf_buffer.getvalue()
