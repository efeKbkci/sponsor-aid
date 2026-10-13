namespace SponsorAid.Application.Interfaces;

public record SemanticMatchResult(
    string Key,         // Eşleşen Enum veya Komut (Örn: "Murdered", "/cancel")
    float Confidence,   // Benzerlik skoru
    bool IsCommand      // Komut mu yoksa cevap mı?
);

public record MatchCandidate(
    string Key,
    string Description,
    IReadOnlyList<string>? Examples = null, // 🚀 Python'a gidecek örnek cümleler
    bool IsCommand = false
);

public interface ISemanticMatcher
{
    Task<SemanticMatchResult?> FindBestMatchAsync(
        string userInput,
        IEnumerable<MatchCandidate> candidates,
        float threshold = 0.65f,
        CancellationToken cancellationToken = default);
}