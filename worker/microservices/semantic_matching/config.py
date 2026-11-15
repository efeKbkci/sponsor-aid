from dataclasses import dataclass
import os

@dataclass(frozen=True)
class SemanticMatcherConfig:
    """Configuration settings for the Turkish semantic sentence embedding model.

    Specifies the transformer model name, default cosine similarity threshold, and target compute device.
    """
    # Uses a Turkish BERT model fine-tuned on NLI and STS-B datasets for accurate Turkish semantic similarity.
    MODEL_NAME: str = os.getenv("SEMANTIC_MODEL_NAME", "emrecan/bert-base-turkish-cased-mean-nli-stsb-tr")
    # Similarity threshold of 0.82 prevents false positive matches during intent classification.
    DEFAULT_THRESHOLD: float = 0.82
    DEVICE: str = "cpu"
