using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using SocialeKaartRag.Core.Chunks;

namespace SocialeKaartRag.Core.Retrieval;

/// <summary>Schema van de index (spec §4.2). "corpus" is het filterveld; "categories" is het boostveld (ADR-0006).</summary>
public static class SearchIndexes
{
    /// <summary>Logische corpusnaam: staat in elke chunk, komt uit de intent-classificatie en is de waarde in het
    /// harde `corpus eq '…'`-filter. Onafhankelijk van de indexversie — die verandert bij een schemawijziging.</summary>
    public const string SocialMapCorpus = "social-map";

    /// <summary>Fysieke indexnaam. v2 (29-08-2026, ADR-0006): meervoudige categorieën + scoring profile
    /// `category-boost`. De v1-index `social-map` blijft staan tot v2 bewezen is (follow-up 24).</summary>
    public const string SocialMap = "social-map-v2";

    public const int EmbeddingDimensions = 1536; // text-embedding-3-small
    private const string VectorProfile = "hnsw-profile";
    private const string VectorAlgo = "hnsw";

    /// <summary>Scoring profile dat de categorie uit de intent als voorkeur meegeeft i.p.v. als harde grens.</summary>
    public const string CategoryBoostProfile = "category-boost";

    /// <summary>Naam van de scoring-parameter; een query geeft 'm mee als "cat-&lt;categorie&gt;".</summary>
    public const string CategoryBoostParameter = "cat";

    /// <summary>Factor waarmee de BM25-score van een document met de gevraagde categorie wordt vermenigvuldigd.</summary>
    public const double CategoryBoost = 2.0;

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
            new SimpleField("categories", SearchFieldDataType.Collection(SearchFieldDataType.String)) { IsFilterable = true, IsFacetable = true },
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
        // Interpolatie bewust `Constant`: een query geeft precies één categorie mee, dus er valt niets te
        // interpoleren tussen "weinig" en "veel" matchende tags. Constant maakt de boost deterministisch en
        // gelijk voor elk document dat de gevraagde categorie draagt — een voorkeur, geen rangorde binnen de tags.
        ScoringProfiles =
        {
            new ScoringProfile(CategoryBoostProfile)
            {
                Functions =
                {
                    new TagScoringFunction("categories", CategoryBoost, new TagScoringParameters(CategoryBoostParameter))
                    {
                        Interpolation = ScoringFunctionInterpolation.Constant,
                    },
                },
            },
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
    public string[] categories { get; set; } = [];
    public string? lastVerified { get; set; }
    public string? headingPath { get; set; }
    public string[] tags { get; set; } = [];
    public object? geo { get; set; }
    public string text { get; set; } = "";
    public float[]? vector { get; set; }

    public static SearchDocumentDto From(Chunk c, float[] vector) => new()
    {
        id = c.Id, corpus = c.Corpus, source = c.Source, sourceId = c.SourceId, sourceUrl = c.SourceUrl,
        retrievedAt = c.RetrievedAt, contentHash = c.ContentHash, category = c.Category, categories = CategoriesOf(c),
        lastVerified = c.LastVerified,
        headingPath = c.HeadingPath, tags = c.Tags, text = c.Text, vector = vector,
        geo = c.Lat is { } lat && c.Lon is { } lon
            ? new Dictionary<string, object> { ["type"] = "Point", ["coordinates"] = new[] { lon, lat } }
            : null,
    };

    /// <summary>Een chunk die alleen een primaire categorie draagt (canaries, oudere bronnen) komt toch met een
    /// gevulde `categories` in de index — anders zou de boost zo'n document nooit kunnen raken.</summary>
    private static string[] CategoriesOf(Chunk c) =>
        c.Categories.Length > 0 ? c.Categories : c.Category is null ? [] : [c.Category];
}
