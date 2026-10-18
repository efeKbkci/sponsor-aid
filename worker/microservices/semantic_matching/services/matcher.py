import torch
from sentence_transformers import SentenceTransformer, util
from collections import OrderedDict
from ..config import SemanticMatcherConfig

class SemanticMatcherService:
    def __init__(self, config: SemanticMatcherConfig | None = None):
        self.config = config or SemanticMatcherConfig()
        print(f"[*] [Semantic Matcher] Türkçe NLI Modeli RAM'e yükleniyor: {self.config.MODEL_NAME}...")
        self.model = SentenceTransformer(self.config.MODEL_NAME, device=self.config.DEVICE)

        self._matrix_cache: OrderedDict[str, dict] = OrderedDict()
        self._max_cache_size = 10
        print("[+] [Semantic Matcher] Hazır!")

    def match(self, user_input: str, candidates: list[dict], threshold: float | None = None) -> dict | None:
        if not user_input or not candidates:
            return None

        effective_threshold = threshold if threshold is not None else self.config.DEFAULT_THRESHOLD

        # 🚀 DÜZELTME: E5 modelindeki "query:" önekini sildik, metni doğrudan ve tertemiz veriyoruz
        input_vec = self.model.encode(user_input.strip().lower(), convert_to_tensor=True, normalize_embeddings=True)

        cache_key = f"{len(candidates)}_{candidates[0].get('key', '')}"

        if cache_key not in self._matrix_cache:
            flat_phrases = []
            phrase_to_candidate = []

            for cand in candidates:
                phrases = [cand.get("description", "")] + cand.get("examples", [])
                for p in phrases:
                    if p:
                        # 🚀 DÜZELTME: E5 modelindeki "passage:" önekini sildik
                        flat_phrases.append(p.strip().lower())
                        phrase_to_candidate.append(cand)

            with torch.no_grad():
                candidate_matrix = self.model.encode(flat_phrases, convert_to_tensor=True, normalize_embeddings=True)

            self._matrix_cache[cache_key] = {
                "matrix": candidate_matrix,
                "mapping": phrase_to_candidate
            }
            
            if len(self._matrix_cache) > self._max_cache_size:
                self._matrix_cache.popitem(last=False)

        self._matrix_cache.move_to_end(cache_key)

        cached_data = self._matrix_cache[cache_key]
        cos_scores = util.cos_sim(input_vec, cached_data["matrix"])[0]

        best_score_tensor, best_idx_tensor = torch.max(cos_scores, dim=0)
        best_score = float(best_score_tensor)

        if best_score >= effective_threshold:
            best_candidate = cached_data["mapping"][int(best_idx_tensor)]
            return {
                "key": best_candidate["key"],
                "confidence": round(best_score, 4),
                "is_command": best_candidate.get("is_command", False)
            }

        return None