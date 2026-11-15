import torch
from sentence_transformers import SentenceTransformer, util
from collections import OrderedDict
from ..config import SemanticMatcherConfig

class SemanticMatcherService:
    """Calculates semantic similarity between user text and candidate intents using sentence transformers.

    Maintains an in-memory LRU cache of candidate embedding matrices to minimize encoding overhead.
    """

    def __init__(self, config: SemanticMatcherConfig | None = None):
        """Initialize the sentence transformer model and candidate embedding cache."""
        self.config = config or SemanticMatcherConfig()
        print(f"[*] [Semantic Matcher] Türkçe NLI Modeli RAM'e yükleniyor: {self.config.MODEL_NAME}...")
        self.model = SentenceTransformer(self.config.MODEL_NAME, device=self.config.DEVICE)

        self._matrix_cache: OrderedDict[str, dict] = OrderedDict()
        self._max_cache_size = 10
        print("[+] [Semantic Matcher] Hazır!")

    def match(self, user_input: str, candidates: list[dict], threshold: float | None = None) -> dict | None:
        """Find the candidate intent with the highest cosine similarity to user input.

        Encodes input, computes cosine similarities against cached candidate matrices, and applies threshold.
        """
        if not user_input or not candidates:
            return None

        # Clean user text by trimming whitespace and normalizing to lowercase.
        clean_input = user_input.strip().lower()
        
        # Reject inputs that contain only whitespace characters.
        if not clean_input:
            return None

        effective_threshold = threshold if threshold is not None else self.config.DEFAULT_THRESHOLD

        # Encode user input directly without prefixes because the Turkish BERT NLI model expects plain text.
        input_vec = self.model.encode(user_input.strip().lower(), convert_to_tensor=True, normalize_embeddings=True)

        cache_key = f"{len(candidates)}_{candidates[0].get('key', '')}"

        # Cache precomputed candidate embedding matrices to avoid re-encoding on every request.
        if cache_key not in self._matrix_cache:
            flat_phrases = []
            phrase_to_candidate = []

            for cand in candidates:
                phrases = [cand.get("description", "")] + cand.get("examples", [])
                for p in phrases:
                    if p:
                        flat_phrases.append(p.strip().lower())
                        phrase_to_candidate.append(cand)

            with torch.no_grad():
                candidate_matrix = self.model.encode(flat_phrases, convert_to_tensor=True, normalize_embeddings=True)

            self._matrix_cache[cache_key] = {
                "matrix": candidate_matrix,
                "mapping": phrase_to_candidate
            }
            
            # Evict the least recently used candidate matrix when cache exceeds capacity.
            if len(self._matrix_cache) > self._max_cache_size:
                self._matrix_cache.popitem(last=False)

        self._matrix_cache.move_to_end(cache_key)

        cached_data = self._matrix_cache[cache_key]
        # Calculate cosine similarity between normalized input and candidate embedding vectors.
        cos_scores = util.cos_sim(input_vec, cached_data["matrix"])[0]

        best_score_tensor, best_idx_tensor = torch.max(cos_scores, dim=0)
        best_score = float(best_score_tensor)

        # Accept matches that meet or exceed the similarity threshold to prevent false positive classifications.
        if best_score >= effective_threshold:
            best_candidate = cached_data["mapping"][int(best_idx_tensor)]
            return {
                "key": best_candidate["key"],
                "confidence": round(best_score, 4),
                "is_command": best_candidate.get("is_command", False)
            }

        return None
