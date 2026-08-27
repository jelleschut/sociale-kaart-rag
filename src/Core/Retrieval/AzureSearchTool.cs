using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using OpenAI.Embeddings;

namespace SocialeKaartRag.Core.Retrieval;

public sealed class AzureSearchTool(SearchIndexClient indexClient, EmbeddingClient embeddings, string corpus) : ISearchTool
{
    public string Name => corpus == SearchIndexes.Kb ? "search_kb" : "search_social_map";
    public string Corpus => corpus;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(SearchQuery query, CancellationToken ct = default)
    {
        var client = indexClient.GetSearchClient(corpus);
        var embedding = (await embeddings.GenerateEmbeddingAsync(query.Text, cancellationToken: ct)).Value.ToFloats().ToArray();

        var options = new SearchOptions
        {
            Size = query.TopK,
            QueryType = SearchQueryType.Simple,
            VectorSearch = new VectorSearchOptions
            {
                Queries = { new VectorizedQuery(embedding) { KNearestNeighborsCount = query.TopK, Fields = { "vector" } } },
            },
            Filter = query.Category is null
                ? $"corpus eq '{corpus}'"
                : $"corpus eq '{corpus}' and category eq '{query.Category.Replace("'", "''")}'",
        };
        foreach (var f in new[] { "id", "corpus", "sourceId", "sourceUrl", "category", "headingPath", "lastVerified", "text" })
            options.Select.Add(f);

        var response = await client.SearchAsync<SearchDocumentDto>(query.Text, options, ct);
        var hits = new List<SearchHit>();
        await foreach (var r in response.Value.GetResultsAsync().WithCancellation(ct))
        {
            var d = r.Document;
            hits.Add(new SearchHit(d.id, d.corpus, d.sourceId, d.sourceUrl, d.category, d.headingPath, d.lastVerified, d.text, r.Score ?? 0));
        }
        return hits;
    }
}
