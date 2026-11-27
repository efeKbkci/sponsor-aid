"""Parses, normalizes, and deduplicates Turkish IBAN candidates from extracted document text."""

import re


class IbanParser:
    """Extracts Turkish IBAN candidates and enforces the strict single-IBAN ambiguity rule."""

    # Matches 'TR' followed by 24 digits with optional spaces, hyphens, or single line breaks between groups.
    # Lookarounds prevent matching mid-token or slicing a prefix from longer alphanumeric identifiers.
    _TURKISH_IBAN_PATTERN = re.compile(
        r"(?<![A-Za-z0-9])[Tt][Rr](?:[ \t-]*\r?\n?[ \t-]*\d){24}(?![A-Za-z0-9])"
    )

    def extract_candidates(self, text: str | None) -> list[str]:
        """Return deduplicated normalized Turkish IBAN candidates ('TR' + 24 digits) in appearance order."""
        if not text:
            return []

        unique_candidates: list[str] = []
        seen: set[str] = set()

        for match in self._TURKISH_IBAN_PATTERN.finditer(text):
            normalized = re.sub(r"[\s-]+", "", match.group(0)).upper()
            if normalized not in seen:
                seen.add(normalized)
                unique_candidates.append(normalized)

        return unique_candidates

    def extract_single_iban(self, text: str | None) -> str | None:
        """Return the normalized IBAN if and only if exactly 1 unique candidate exists; otherwise None."""
        candidates = self.extract_candidates(text)
        if len(candidates) == 1:
            return candidates[0]
        return None
