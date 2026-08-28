using System.Globalization;
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
            Filter = BuildFilter(corpus, query.Category, query.Near, query.RadiusKm),
        };
        foreach (var f in new[] { "id", "corpus", "sourceId", "sourceUrl", "category", "headingPath", "lastVerified", "text", "tags" })
            options.Select.Add(f);

        var response = await client.SearchAsync<SearchDocumentDto>(query.Text, options, ct);
        var hits = new List<SearchHit>();
        await foreach (var r in response.Value.GetResultsAsync().WithCancellation(ct))
        {
            var d = r.Document;
            hits.Add(new SearchHit(d.id, d.corpus, d.sourceId, d.sourceUrl, d.category, d.headingPath, d.lastVerified, d.text, r.Score ?? 0, d.tags));
        }
        return hits;
    }

    /// <summary>OData-filter (spec §4.2): corpus altijd, category optioneel, geo.distance optioneel (radius in km).</summary>
    public static string BuildFilter(string corpus, string? category, GeoPoint? near, double radiusKm)
    {
        var filter = $"corpus eq '{corpus}'";
        if (category is not null)
            filter += $" and category eq '{category.Replace("'", "''")}'";
        if (near is not null)
        {
            // De geo-filter geldt alleen voor OSM-records: SC-producten dragen één gemeente-centroïde, geen
            // locatie, en horen altijd binnen de eigen gemeente beschikbaar te zijn (dus ontsnappen ze eraan).
            var point = $"geography'POINT({near.Lon.ToString(CultureInfo.InvariantCulture)} {near.Lat.ToString(CultureInfo.InvariantCulture)})'";
            filter += $" and (geo.distance(geo, {point}) le {radiusKm.ToString(CultureInfo.InvariantCulture)} or tags/any(t: t eq 'bron:sc'))";
        }
        return filter;
    }
}
