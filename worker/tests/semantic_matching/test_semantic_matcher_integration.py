import pytest
from fastapi.testclient import TestClient

def test_match_endpoint_accurate_phrase_returns_brain_hemorrhage_with_high_confidence(
    test_client: TestClient,
    father_death_reasons_catalog: list[dict],
) -> None:
    # Arrange
    payload = {
        "user_input": "tansiyondan beyin kanaması geçirdi",
        "candidates": father_death_reasons_catalog,
        "threshold": 0.82,
    }

    # Act
    response = test_client.post("/api/semantic/match", json=payload)

    # Assert
    assert response.status_code == 200, f"Expected 200 OK, got: {response.text}"
    data = response.json()
    assert data is not None, "Output must not be null for a direct semantically relevant phrase"
    assert data["key"] == "BrainHemorrhage", f"Expected key 'BrainHemorrhage', got: {data.get('key')}"
    assert data["confidence"] >= 0.82, f"Confidence {data.get('confidence')} should be >= 0.82"
    assert data["is_command"] is False


def test_match_endpoint_unrelated_phrase_returns_null(
    test_client: TestClient,
    father_professions_catalog: list[dict],
) -> None:
    # Arrange
    payload = {
        "user_input": "uzaylılar dünyayı istila etti",
        "candidates": father_professions_catalog,
        "threshold": 0.65,
    }

    # Act
    response = test_client.post("/api/semantic/match", json=payload)

    # Assert
    assert response.status_code == 200, f"Expected 200 OK, got: {response.text}"
    assert response.json() is None, "Irrelevant query must produce null without forcing a false-positive match"


def test_match_endpoint_whitespace_input_returns_null_immediately(
    test_client: TestClient,
    father_professions_catalog: list[dict],
) -> None:
    # Arrange
    payload = {
        "user_input": "     ",
        "candidates": father_professions_catalog,
        "threshold": 0.65,
    }

    # Act
    response = test_client.post("/api/semantic/match", json=payload)

    # Assert
    assert response.status_code == 200, f"Expected 200 OK, got: {response.text}"
    assert response.json() is None, "Pure whitespace query must be rejected at input gate with null result"