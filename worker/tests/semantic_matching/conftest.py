import pytest
from fastapi import FastAPI
from fastapi.testclient import TestClient

from microservices.semantic_matching.handler import router as semantic_router
from microservices.semantic_matching.services.matcher import SemanticMatcherService


@pytest.fixture(scope="session")
def real_semantic_service() -> SemanticMatcherService:
    """Provides a shared SemanticMatcherService instance running the actual model."""
    return SemanticMatcherService()


@pytest.fixture(scope="session")
def test_client() -> TestClient:
    """Provides a FastAPI TestClient wired to the actual semantic router."""
    app = FastAPI(title="Semantic Matching Integration Test App")
    app.include_router(semantic_router)
    return TestClient(app)


@pytest.fixture
def father_professions_catalog() -> list[dict]:
    """Catalog items from FatherProfessions."""
    return [
        {
            "key": "Driver",
            "description": "Şoför",
            "examples": ["taksiciydi", "kamyon sürüyordu", "tır şoförüydü", "servis çekiyordu", "dolmuşçu"],
            "is_command": False,
        },
        {
            "key": "Worker",
            "description": "İşçi",
            "examples": ["fabrikada çalışıyordu", "asgari ücretli işçiydi", "tekstilde çalışıyordu", "ameleydi", "inşaat işçisi"],
            "is_command": False,
        },
        {
            "key": "Chef",
            "description": "Aşçı",
            "examples": ["restoranda çalışıyordu", "aşçılık yapıyordu", "mutfakta çalışandı"],
            "is_command": False,
        },
        {
            "key": "Lawyer",
            "description": "Avukat",
            "examples": ["avukatlık yapıyordu", "hukuk bürosunun sahibiydi"],
            "is_command": False,
        },
    ]


@pytest.fixture
def father_death_reasons_catalog() -> list[dict]:
    """Catalog items from FatherDeathReasons."""
    return [
        {
            "key": "BrainHemorrhage",
            "description": "Beyin Kanaması",
            "examples": ["beynine pıhtı attı", "tansiyondan beyin kanaması geçirdi", "felç sonrası kanama"],
            "is_command": False,
        },
        {
            "key": "HeartAttack",
            "description": "Kalp Krizi",
            "examples": ["kalbi durdu", "kalp spazmı geçirdi", "enfarktüs", "ani kalp durması"],
            "is_command": False,
        },
        {
            "key": "TrafficAccident",
            "description": "Trafik Kazası",
            "examples": ["araba çarptı", "araç takla attı", "motor kazası yaptı"],
            "is_command": False,
        },
    ]