import torch
from sentence_transformers import SentenceTransformer, util
from collections import OrderedDict
from ..config import SemanticMatcherConfig

class SemanticMatcherService:
    def __init__(self, config: SemanticMatcherConfig | None = None):
        self.config = config or SemanticMatcherConfig()
        print(f"[*] [Semantic Matcher] Model RAM'e yükleniyor: {self.config.MODEL_NAME}...")
        self.model = SentenceTransformer(self.config.MODEL_NAME, device=self.config.DEVICE)

        # Sınırsız dict yerine maksimum 512 eleman tutan LRU Cache yapısı:
        self._cache: OrderedDict[str, torch.Tensor] = OrderedDict()
        self._max_cache_size: int = 512
        print("[+] [Semantic Matcher] Model hazır!")

    def _get_embedding(self, text: str, is_query: bool = False) -> torch.Tensor:
        prefix = "query: " if is_query else "passage: "
        prefixed_text = f"{prefix}{text.strip().lower()}"

        # 1. Önbellekte varsa, elemanı "en son kullanılan" olarak sona taşı ve döndür
        if prefixed_text in self._cache:
            self._cache.move_to_end(prefixed_text)
            return self._cache[prefixed_text]

        # 2. Önbellekte yoksa modeli çalıştır (CPU üzerinde, gradyan hesaplamadan)
        with torch.no_grad():
            embedding = self.model.encode(
                prefixed_text,
                convert_to_tensor=True,
                normalize_embeddings=True
            )

        # 3. Yeni sonucu önbelleğe ekle; sınır (512) aşıldıysa en eskiyi (ilk gireni) sil
        self._cache[prefixed_text] = embedding
        if len(self._cache) > self._max_cache_size:
            self._cache.popitem(last=False)

        return embedding

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