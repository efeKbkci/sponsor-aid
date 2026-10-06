import os

# Model Yolları (Os.path.join ile işletim sistemi bağımsız yapıyoruz)
BASE_DIR = os.path.dirname(os.path.abspath(__file__))
SEGMENTER_MODEL_PATH = os.path.join(BASE_DIR,"models", "id_segmenter.pt")
ROTATOR_MODEL_PATH = os.path.join(BASE_DIR, "models", "id_orientation_cls.pt")