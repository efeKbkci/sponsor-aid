import torch
from sentence_transformers import SentenceTransformer, util
from ..config import SemanticMatcherConfig

class SemanticMatcherService:
    def __init__(self, config: SemanticMatcherConfig | None = None):
        self.config = config or SemanticMatcherConfig()
        print(f"[*] [Semantic Matcher] Model RAM'e yükleniyor: {self.config.MODEL_NAME}...")
        self.model = SentenceTransformer(self.config.MODEL_NAME, device=self.config.DEVICE)
        self._cache: dict[str, torch.Tensor] = {}
        print("[+] [Semantic Matcher] Model hazır!")

    def _get_embedding(self, text: str, is_query: bool = False) -> torch.Tensor:
        prefix = "query: " if is_query else "passage: "
        formatted_text = f"{prefix}{text.strip().lower()}"

        if formatted_text not in self._cache:
            self._cache[formatted_text] = self.model.encode(
                formatted_text, 
                convert_to_tensor=True, 
                device=self.config.DEVICE,
                normalize_embeddings=True
            )
        return self._cache[formatted_text]

    def match(self, user_input: str, candidates: list[dict], threshold: float | None = None) -> dict | None:
        if not user_input or not candidates:
            return None

        effective_threshold = threshold if threshold is not None else self.config.DEFAULT_THRESHOLD
        input_vec = self._get_embedding(user_input, is_query=True)

        best_candidate = None
        best_score = -1.0

        for candidate in candidates:
            # 1. Ana açıklama ve varsa few-shot örneklerini tek bir havuzda topla
            target_phrases = [candidate.get("description", "")] + candidate.get("examples", [])
            candidate_max_score = -1.0

            # 2. Kullanıcı girdisini bu adayın tüm örnekleriyle yarıştır
            for phrase in target_phrases:
                if not phrase:
                    continue
                cand_vec = self._get_embedding(phrase, is_query=False)
                score = float(util.cos_sim(input_vec, cand_vec)[0][0])
                if score > candidate_max_score:
                    candidate_max_score = score

            # 3. Bu adayın en güçlü örneği genel liderden daha iyiyse lideri güncelle
            if candidate_max_score > best_score:
                best_score = candidate_max_score
                best_candidate = candidate

        if best_score >= effective_threshold and best_candidate:
            return {
                "key": best_candidate["key"],
                "confidence": round(best_score, 4),
                "is_command": best_candidate.get("is_command", False)
            }

        return None