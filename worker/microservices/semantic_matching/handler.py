from fastapi import APIRouter
from pydantic import BaseModel, Field
from .services.matcher import SemanticMatcherService

router = APIRouter(prefix="/api/semantic", tags=["Semantic Matcher"])
service = SemanticMatcherService()

class CandidateDto(BaseModel):
    """Represents a target candidate intent with descriptive text and sample utterances."""
    key: str
    description: str
    examples: list[str] = Field(default_factory=list)  # Example utterances used to broaden embedding match coverage.
    is_command: bool = False

class MatchRequest(BaseModel):
    """Input payload for semantic text matching requests.

    A threshold of 0.65 provides a baseline cutoff when no higher threshold is passed.
    """
    user_input: str
    candidates: list[CandidateDto]
    threshold: float = 0.65

class MatchResponse(BaseModel):
    """Result payload representing the best matched candidate and its similarity confidence."""
    key: str
    confidence: float
    is_command: bool

@router.post("/match", response_model=MatchResponse | None)
def match_text(request: MatchRequest):
    """Match user input against candidate intents using cosine similarity of sentence embeddings.

    Returns the top candidate exceeding the threshold, or None if no candidate matches closely enough.
    """
    candidates_dict = [c.model_dump() for c in request.candidates]
    return service.match(request.user_input, candidates_dict, request.threshold)

if __name__ == "__main__":
    import uvicorn
    from fastapi import FastAPI

    app = FastAPI(title="Semantic Matcher Test API")
    app.include_router(router)
    uvicorn.run(app, host="127.0.0.1", port=8001)
