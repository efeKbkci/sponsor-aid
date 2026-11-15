import cv2
import numpy as np
import re
from ..config import BarcodeConfig

class BarcodeScannerService:
    """Detects and extracts official verification codes from document QR codes.

    Uses the WeChat QR engine with super-resolution networks to read noisy or low-resolution images.
    """

    def __init__(self):
        self.config = BarcodeConfig()
        self.detector = cv2.wechat_qrcode.WeChatQRCode(
            self.config.DETECT_PROTO_PATH, 
            self.config.DETECT_MODEL_PATH,
            self.config.SR_PROTO_PATH, 
            self.config.SR_MODEL_PATH
        )
        # Turkish e-Government verification codes follow four groups of four alphanumeric characters.
        self.barcode_pattern = re.compile(r"[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}")

    def extract(self, image) -> str:
        """Scan the image and extract the matched e-Devlet verification code.

        Returns the matched code string or raises an exception if detection fails.
        """
        # WeChat QR detector uses super-resolution to decode blurred or small barcodes.
        res, _ = self.detector.detectAndDecode(image)

        if res:
            for qr_text in res:
                # Scans text content across all detected codes to isolate the verification pattern.
                match = self.barcode_pattern.search(qr_text.upper())
                if match:
                    return match.group(0)

        # Rejects document when verification code cannot be recovered because backend validation requires this key.
        raise ValueError("Belgedeki karekod (QR) okunamadı veya ekran yansıması çok fazla. Lütfen belgenin PDF halini indirmeyi veya yansımasız, net bir fotoğrafını çekmeyi deneyin.")
