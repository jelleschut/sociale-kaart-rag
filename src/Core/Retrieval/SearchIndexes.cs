using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using SocialeKaartRag.Core.Chunks;

namespace SocialeKaartRag.Core.Retrieval;

/// <summary>Schema van de twee indexen (spec §4.2). Zelfde document-model; "corpus" is het filterveld.</summary>
public static class SearchIndexes
{
    public const string Kb = "kb";
    public const string SocialMap = "social-map";
    public const int EmbeddingDimensions = 1536; // text-embedding-3-small
    private const string VectorProfile = "hnsw-profile";
    private const string VectorAlgo = "hnsw";

    public static SearchIndex Define(string name) => new(name)
    {
        Fields =
        [
            new SimpleField("id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
            new SimpleField("corpus", SearchFieldDataType.String) { IsFilterable = true, IsFacetable = true },
            new SimpleField("source", SearchFieldDataType.String) { IsFilterable = true },
            new SimpleField("sourceId", SearchFieldDataType.String) { IsFilterable = true },
            new SimpleField("sourceUrl", SearchFieldDataType.String),
            new SimpleField("retrievedAt", SearchFieldDataType.DateTimeOffset) { IsFilterable = true },
            new SimpleField("contentHash", SearchFieldDataType.String),
            new SimpleField("category", SearchFieldDataType.String) { IsFilterable = true, IsFacetable = true },
            new SimpleField("lastVerified", SearchFieldDataType.String),
            new SearchableField("headingPath") { AnalyzerName = LexicalAnalyzerName.NlMicrosoft },
            new SearchableField("tags", collection: true) { IsFilterable = true },
            new SimpleField("geo", SearchFieldDataType.GeographyPoint) { IsFilterable = true },
            new SearchableField("text") { AnalyzerName = LexicalAnalyzerName.NlMicrosoft },
            new VectorSearchField("vector", EmbeddingDimensions, VectorProfile),
        ],
        VectorSearch = new VectorSearch
        {
            Profiles = { new VectorSearchProfile(VectorProfile, VectorAlgo) },
            Algorithms = { new HnswAlgorithmConfiguration(VectorAlgo) },
        },
    };
}

/// <summary>Document zoals het in de index staat. Geo als GeoJSON-punt (Search-formaat).</summary>
// Geen `required` op de members: de Search SDK deserialiseert ook wanneer $select
// een subset van velden teruggeeft, en `required` laat System.Text.Json dan falen
// op de ontbrekende (niet-geselecteerde) velden (SDK 12.0-aanpassing, zie rapport).
public sealed class SearchDocumentDto
{
    public string id { get; set; } = "";
    public string corpus { get; set; } = "";
    public string source { get; set; } = "";
    public string sourceId { get; set; } = "";
    public string? sourceUrl { get; set; }
    public DateTimeOffset retrievedAt { get; set; }
    public string contentHash { get; set; } = "";
    public string? category { get; set; }
    public string? lastVerified { get; set; }
    public string? headingPath { get; set; }
    public string[] tags { get; set; } = [];
    public object? geo { get; set; }
    public string text { get; set; } = "";
    public float[]? vector { get; set; }

    public static SearchDocumentDto From(Chunk c, float[] vector) => new()
    {
        id = c.Id, corpus = c.Corpus, source = c.Source, sourceId = c.SourceId, sourceUrl = c.SourceUrl,
        retrievedAt = c.RetrievedAt, contentHash = c.ContentHash, category = c.Category, lastVerified = c.LastVerified,
        headingPath = c.HeadingPath, tags = c.Tags, text = c.Text, vector = vector,
        geo = c.Lat is { } lat && c.Lon is { } lon
            ? new Dictionary<string, object> { ["type"] = "Point", ["coordinates"] = new[] { lon, lat } }
            : null,
    };
}
