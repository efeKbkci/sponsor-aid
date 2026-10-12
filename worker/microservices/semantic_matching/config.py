from dataclasses import dataclass
import os

@dataclass(frozen=True)
class SemanticMatcherConfig:
    # MiniLM yerine multilingual-e5-small'a geçtik
    MODEL_NAME: str = os.getenv("SEMANTIC_MODEL_NAME", "intfloat/multilingual-e5-small")
    DEFAULT_THRESHOLD: float = 0.65
    DEVICE: str = "cpu"