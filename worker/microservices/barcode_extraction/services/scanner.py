import cv2
import numpy as np
import re
from ..config import BarcodeConfig

class BarcodeScannerService:
    def __init__(self):
        self.config = BarcodeConfig()
        self.detector = cv2.wechat_qrcode.WeChatQRCode(
            self.config.DETECT_PROTO_PATH, 
            self.config.DETECT_MODEL_PATH,
            self.config.SR_PROTO_PATH, 
            self.config.SR_MODEL_PATH
        )
        # E-Devlet Barkod Standardı: 4'erli gruplar halinde harf/rakam (Örn: NV02-NOLP-3N0Y-N8O8)
        self.barcode_pattern = re.compile(r"[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}")

    def extract(self, image) -> str:
        # WeChat Motoru ile tara
        res, _ = self.detector.detectAndDecode(image)

        # Sonuç bulundu mu?
        if res:
            for qr_text in res:
                # E-Devlet barkodunu cımbızla
                match = self.barcode_pattern.search(qr_text.upper())
                if match:
                    return match.group(0)

        # Eğer OpenCV'nin bu 4 agresif filtresine rağmen QR okunamadıysa, kuralımızı işletip reddediyoruz.
        raise ValueError("Belgedeki karekod (QR) okunamadı veya ekran yansıması çok fazla. Lütfen belgenin PDF halini indirmeyi veya yansımasız, net bir fotoğrafını çekmeyi deneyin.")