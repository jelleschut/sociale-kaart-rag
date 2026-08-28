namespace SocialeKaartRag.Core.Retrieval;

public sealed record SearchHit(
    string Id, string Corpus, string SourceId, string? SourceUrl, string? Category,
    string? HeadingPath, string? LastVerified, string Text, double Score, string[]? Tags = null)
{
    public string? Attribution => AttributionFromTags(Tags);

    public static string? AttributionFromTags(string[]? tags) =>
        tags?.FirstOrDefault(t => t.StartsWith("attribution:", StringComparison.Ordinal))?["attribution:".Length..];
}

public sealed record SearchQuery(string Text, string? Category = null, GeoPoint? Near = null, double RadiusKm = 5, int TopK = 6);

/// <summary>Enige tool-soort die de orchestrator kent (spec §4.2/§4.3 allow-list).</summary>
public interface ISearchTool
{
    string Name { get; }      // "search_social_map"
    string Corpus { get; }    // "social-map"
    Task<IReadOnlyList<SearchHit>> SearchAsync(SearchQuery query, CancellationToken ct = default);
}
