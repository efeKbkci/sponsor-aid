from fastapi import APIRouter
from pydantic import BaseModel, Field
from .services.matcher import SemanticMatcherService

router = APIRouter(prefix="/api/semantic", tags=["Semantic Matcher"])
service = SemanticMatcherService()

class CandidateDto(BaseModel):
    key: str
    description: str
    examples: list[str] = Field(default_factory=list) # 🚀 Few-shot örnek kalıpları
    is_command: bool = False

class MatchRequest(BaseModel):
    user_input: str
    candidates: list[CandidateDto]
    threshold: float = 0.65

class MatchResponse(BaseModel):
    key: str
    confidence: float
    is_command: bool

@router.post("/match", response_model=MatchResponse | None)
def match_text(request: MatchRequest):
    candidates_dict = [c.model_dump() for c in request.candidates]
    return service.match(request.user_input, candidates_dict, request.threshold)

if __name__ == "__main__":
    import uvicorn
    from fastapi import FastAPI

    app = FastAPI(title="Semantic Matcher Test API")
    app.include_router(router)
    uvicorn.run(app, host="127.0.0.1", port=8001)