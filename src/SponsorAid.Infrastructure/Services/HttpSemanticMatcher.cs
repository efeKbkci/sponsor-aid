using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Infrastructure.Services;

public sealed class HttpSemanticMatcher(HttpClient httpClient, IConfiguration configuration) : ISemanticMatcher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Uri _endpoint = CreateEndpoint(configuration);

    public async Task<SemanticMatchResult?> FindBestMatchAsync(
        string userInput,
        IEnumerable<MatchCandidate> candidates,
        float threshold = 0.65f,
        CancellationToken cancellationToken = default)
    {
        var request = new MatchRequest(
            userInput,
            candidates.Select(candidate => new CandidateRequest(
                candidate.Key,
                candidate.Description,
                candidate.Examples ?? Array.Empty<string>(),
                candidate.IsCommand)).ToArray(),
            threshold);

        using var response = await httpClient.PostAsJsonAsync(_endpoint, request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var match = await response.Content.ReadFromJsonAsync<MatchResponse>(JsonOptions, cancellationToken);
        return match is null
            ? null
            : new SemanticMatchResult(match.Key, match.Confidence, match.IsCommand);
    }

    private static Uri CreateEndpoint(IConfiguration configuration)
    {
        var workerApiUrl = configuration["WORKER_API_URL"]
            ?? Environment.GetEnvironmentVariable("WORKER_API_URL")
            ?? throw new InvalidOperationException("WORKER_API_URL is not configured.");

        return new Uri($"{workerApiUrl.TrimEnd('/')}/api/semantic/match");
    }

    private sealed record MatchRequest(
        [property: JsonPropertyName("user_input")] string UserInput,
        [property: JsonPropertyName("candidates")] IReadOnlyList<CandidateRequest> Candidates,
        [property: JsonPropertyName("threshold")] float Threshold);

    private sealed record CandidateRequest(
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("examples")] IReadOnlyList<string> Examples,
        [property: JsonPropertyName("is_command")] bool IsCommand);

    private sealed record MatchResponse(
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("confidence")] float Confidence,
        [property: JsonPropertyName("is_command")] bool IsCommand);
}
