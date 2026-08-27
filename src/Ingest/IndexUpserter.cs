using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using SocialeKaartRag.Core.Chunks;
using SocialeKaartRag.Core.Retrieval;

namespace SocialeKaartRag.Ingest;

public sealed class IndexUpserter(SearchIndexClient indexClient, Embedder embedder)
{
    public async Task EnsureIndexAsync(string name, CancellationToken ct = default)
    {
        await indexClient.CreateOrUpdateIndexAsync(SearchIndexes.Define(name), cancellationToken: ct);
        Console.WriteLine($"index '{name}' aanwezig");
    }

    /// <summary>Idempotente upsert op id (spec §4.1). Batches van 100.</summary>
    public async Task<int> UpsertAsync(string indexName, IReadOnlyList<Chunk> chunks, CancellationToken ct = default)
    {
        var client = indexClient.GetSearchClient(indexName);
        var total = 0;
        foreach (var batch in chunks.Chunk(100))
        {
            var vectors = await embedder.EmbedAsync(batch.Select(c => c.Text).ToList(), ct);
            var docs = batch.Select((c, i) => SearchDocumentDto.From(c, vectors[i])).ToList();
            var result = await client.IndexDocumentsAsync(IndexDocumentsBatch.MergeOrUpload(docs), cancellationToken: ct);
            var failed = result.Value.Results.Where(r => !r.Succeeded).ToList();
            if (failed.Count > 0)
                throw new InvalidOperationException($"{failed.Count} documenten geweigerd; eerste: {failed[0].Key}: {failed[0].ErrorMessage}");
            total += docs.Count;
            Console.WriteLine($"  {total}/{chunks.Count} geüpload");
        }
        return total;
    }
}
