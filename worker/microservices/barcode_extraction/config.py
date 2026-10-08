from dataclasses import dataclass
import os

@dataclass(frozen=True)
class BarcodeConfig:
    BASE_DIR = os.path.dirname(os.path.abspath(__file__))
    DETECT_MODEL_PATH: str = os.path.join(BASE_DIR, "models", "detect.caffemodel")
    DETECT_PROTO_PATH: str = os.path.join(BASE_DIR, "models", "detect.prototxt")
    SR_MODEL_PATH:str = os.path.join(BASE_DIR, "models", "sr.caffemodel")
    SR_PROTO_PATH:str = os.path.join(BASE_DIR, "models", "sr.prototxt")