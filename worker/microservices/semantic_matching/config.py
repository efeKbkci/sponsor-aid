from dataclasses import dataclass
import os

@dataclass(frozen=True)
class SemanticMatcherConfig:
    # Multilingual (Çok Dilli) model yerine, NLI verisetiyle eğitilmiş %100 Türkçe modele geçiyoruz    
    MODEL_NAME: str = os.getenv("SEMANTIC_MODEL_NAME", "emrecan/bert-base-turkish-cased-mean-nli-stsb-tr")
    DEFAULT_THRESHOLD: float = 0.82
    DEVICE: str = "cpu"