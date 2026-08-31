using SocialeKaartRag.Core.Chunks;
using SocialeKaartRag.Core.Retrieval;
using SocialeKaartRag.Ingest;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SocialeKaartRag.Eval;

public sealed class Canary { public required string Id { get; set; } public required string Name { get; set; } public required string Municipality { get; set; } public required string Category { get; set; } public required string Text { get; set; } }

/// <summary>Tijdelijke injectie-documenten in de sociale-kaart-index; seed vóór de run, verwijder erna (ook bij fouten).</summary>
public static class Canaries
{
    private static readonly IDeserializer Yaml = new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build();
    private static readonly Dictionary<string, (double Lat, double Lon)> Centroids = new() { ["Den Haag"] = (52.072, 4.293), ["Zoetermeer"] = (52.061, 4.490) };

    public static List<Canary> Load(string path) => Yaml.Deserialize<List<Canary>>(File.ReadAllText(path)) ?? [];

    public static List<Chunk> ToChunks(IEnumerable<Canary> canaries) => canaries.Select(c =>
    {
        if (!Centroids.TryGetValue(c.Municipality, out var centroid))
            throw new InvalidOperationException($"onbekende gemeente '{c.Municipality}' in canaries.yaml");
        var (lat, lon) = centroid;
        return new Chunk
        {
            Id = Chunk.MakeId(SearchIndexes.SocialMapCorpus, "eval:" + c.Id), Corpus = SearchIndexes.SocialMapCorpus, Source = "eval", SourceId = "eval:" + c.Id,
            SourceUrl = "https://github.com/jelleschut/sociale-kaart-rag/blob/main/eval/canaries.yaml", RetrievedAt = DateTimeOffset.UtcNow,
            ContentHash = Chunk.HashContent(c.Text), Category = c.Category, Categories = [c.Category], HeadingPath = c.Name,
            Tags = ["gemeente:" + c.Municipality, "bron:eval", "attribution:eval-canary (tijdelijk testdocument)"], Lat = lat, Lon = lon, Text = c.Text.Trim(),
        };
    }).ToList();

    public static Task SeedAsync(IndexUpserter upserter, IEnumerable<Chunk> chunks) => upserter.UpsertAsync(SearchIndexes.SocialMap, chunks.ToList());

    public static async Task CleanupAsync(Azure.Search.Documents.Indexes.SearchIndexClient client, IEnumerable<Chunk> chunks)
    {
        var search = client.GetSearchClient(SearchIndexes.SocialMap);
        await search.DeleteDocumentsAsync("id", chunks.Select(c => c.Id).ToList());
    }
}
