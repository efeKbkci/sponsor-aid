import time
import pytest
from microservices.semantic_matching.services.matcher import SemanticMatcherService


def test_match_repeated_candidate_list_uses_cache_and_executes_significantly_faster(
    real_semantic_service: SemanticMatcherService,
    father_professions_catalog: list[dict],
) -> None:
    # Arrange
    first_query = "babam tır şoförüydü"
    second_query = "babam fabrikada ameleydi"
    expected_cache_key = f"{len(father_professions_catalog)}_{father_professions_catalog[0]['key']}"

    # Ensure clean state for this candidate set
    real_semantic_service._matrix_cache.pop(expected_cache_key, None)

    # Act: Cold execution (calculates candidate matrix and caches it)
    start_cold = time.perf_counter()
    cold_result = real_semantic_service.match(first_query, father_professions_catalog, threshold=0.65)
    cold_duration = time.perf_counter() - start_cold

    # Act: Warm execution (hits memory cache for candidate matrix)
    start_warm = time.perf_counter()
    warm_result = real_semantic_service.match(second_query, father_professions_catalog, threshold=0.65)
    warm_duration = time.perf_counter() - start_warm

    # Assert
    assert cold_result is not None, "First query should successfully match"
    assert cold_result["key"] == "Driver", "Expected 'Driver' for 'tır şoförüydü'"

    assert warm_result is not None, "Second query should successfully match"
    assert warm_result["key"] == "Worker", "Expected 'Worker' for 'fabrikada ameleydi'"

    assert expected_cache_key in real_semantic_service._matrix_cache, "Candidate matrix must persist in memory cache"
    assert warm_duration < cold_duration, (
        f"Warm query ({warm_duration:.4f}s) should execute faster than cold query ({cold_duration:.4f}s)"
    )


def test_match_cache_evicts_oldest_entry_when_capacity_exceeds_max_limit(
    real_semantic_service: SemanticMatcherService,
) -> None:
    # Arrange: Build 11 distinct candidate lists
    candidate_lists = [
        [{"key": f"Category_{i}", "description": f"Tanım {i}", "examples": [f"Örnek {i}"]}]
        for i in range(1, 12)
    ]
    first_cache_key = f"1_{candidate_lists[0][0]['key']}"
    last_cache_key = f"1_{candidate_lists[-1][0]['key']}"

    # Act: Sequentially trigger queries for all 11 categories
    for i, candidates in enumerate(candidate_lists, start=1):
        real_semantic_service.match(f"test sorgusu {i}", candidates, threshold=0.50)

    # Assert: State-based verification on cache container
    current_cache_size = len(real_semantic_service._matrix_cache)
    assert current_cache_size == 10, f"Cache size must stay at maximum limit of 10, got {current_cache_size}"
    assert first_cache_key not in real_semantic_service._matrix_cache, (
        f"Oldest key '{first_cache_key}' must be evicted from cache"
    )
    assert last_cache_key in real_semantic_service._matrix_cache, (
        f"Newest key '{last_cache_key}' must reside in cache"
    )