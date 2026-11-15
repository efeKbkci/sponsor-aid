"""Configuration paths for identity card image processing models.

Resolves model file paths for segmentation and orientation classification networks.
"""
import os

# Build paths with os.path.join to ensure compatibility across Linux and Windows hosts.
BASE_DIR = os.path.dirname(os.path.abspath(__file__))
SEGMENTER_MODEL_PATH = os.path.join(BASE_DIR, "models", "id_segmenter.pt")
ROTATOR_MODEL_PATH = os.path.join(BASE_DIR, "models", "id_orientation_cls.pt")
