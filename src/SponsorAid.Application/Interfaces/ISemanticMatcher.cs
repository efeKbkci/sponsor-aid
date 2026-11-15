namespace SponsorAid.Application.Interfaces;

/// <summary>
/// Represents the result of matching user input against known options using text similarity.
/// </summary>
/// <param name="Key">Matched identifier representing an enum value or command.</param>
/// <param name="Confidence">Similarity score indicating match quality.</param>
/// <param name="IsCommand">Indicates whether the matched input represents a flow command rather than a form value.</param>
public record SemanticMatchResult(
    string Key,
    float Confidence,
    bool IsCommand
);

/// <summary>
/// Represents a possible option or command evaluated during semantic matching.
/// </summary>
/// <param name="Key">Identifier of the option or command.</param>
/// <param name="Description">Description of the option purpose.</param>
/// <param name="Examples">Sample phrases sent to the language model for similarity scoring.</param>
/// <param name="IsCommand">Indicates whether the candidate triggers a system action.</param>
public record MatchCandidate(
    string Key,
    string Description,
    IReadOnlyList<string>? Examples = null,
    bool IsCommand = false
);

/// <summary>
/// Matches unstructured user answers to expected domain values using semantic embeddings.
/// </summary>
public interface ISemanticMatcher
{
    /// <summary>
    /// Finds the best matching candidate for a user input using semantic similarity.
    /// </summary>
    /// <param name="userInput">Text input supplied by the user.</param>
    /// <param name="candidates">Candidate choices available for the current question.</param>
    /// <param name="threshold">Minimum similarity score needed to accept a match.</param>
    /// <param name="cancellationToken">Token to cancel the matching task.</param>
    /// <returns>The best match if it meets the threshold; otherwise null.</returns>
    Task<SemanticMatchResult?> FindBestMatchAsync(
        string userInput,
        IEnumerable<MatchCandidate> candidates,
        float threshold = 0.65f,
        CancellationToken cancellationToken = default);
}
