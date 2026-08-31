using System.Globalization;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using OpenAI.Embeddings;

namespace SocialeKaartRag.Core.Retrieval;

/// <summary>Hybride zoektool (BM25 + vector, RRF-gefuseerd) over één index. `indexName` is de fysieke index,
/// `corpus` de logische corpusnaam die in het harde filter staat en waarop de orchestrator routeert.</summary>
public sealed class AzureSearchTool(SearchIndexClient indexClient, EmbeddingClient embeddings, string indexName, string corpus) : ISearchTool
{
    public string Name => "search_social_map";
    public string Corpus => corpus;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(SearchQuery query, CancellationToken ct = default)
    {
        var client = indexClient.GetSearchClient(indexName);
        var embedding = (await embeddings.GenerateEmbeddingAsync(query.Text, cancellationToken: ct)).Value.ToFloats().ToArray();

        var options = new SearchOptions
        {
            Size = query.TopK,
            QueryType = SearchQueryType.Simple,
            VectorSearch = new VectorSearchOptions
            {
                Queries = { new VectorizedQuery(embedding) { KNearestNeighborsCount = query.TopK, Fields = { "vector" } } },
            },
            Filter = BuildFilter(corpus, query.Near, query.RadiusKm),
        };
        // De categorie uit de intent is een voorkeur, geen grens (ADR-0006): een tag-boost op `categories`, niet
        // een `category eq '…'`-filter. Reikwijdte: een scoring profile werkt op de tekst-(BM25-)tak van de
        // hybride query; de vectortak wordt niet gescoord door het profiel en RRF fuseert beide rangordes daarna.
        // Dat is precies het gewenste effect — de categorie duwt passende documenten omhoog in de tekstrangorde
        // en dus in de fusie, maar kan nooit een document uitsluiten dat de vectortak wél vindt.
        if (query.Category is not null)
        {
            options.ScoringProfile = SearchIndexes.CategoryBoostProfile;
            options.ScoringParameters.Add($"{SearchIndexes.CategoryBoostParameter}-{query.Category}");
        }
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

    /// <summary>OData-filter (spec §4.2): corpus altijd, geo.distance optioneel (radius in km). De categorie zit
    /// hier bewust niet meer in — die is sinds ADR-0006 een boost via het scoring profile.</summary>
    public static string BuildFilter(string corpus, GeoPoint? near, double radiusKm)
    {
        var filter = $"corpus eq '{corpus}'";
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
