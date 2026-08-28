using System.Text.Json;
using Azure.Search.Documents.Indexes;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Embeddings;
using SocialeKaartRag.Core;
using SocialeKaartRag.Core.Retrieval;
using SocialeKaartRag.Ingest;
using SocialeKaartRag.Ingest.Sources;

var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
var services = new ServiceCollection().AddAzureClients(config);
services.AddSingleton<Embedder>();
services.AddSingleton<IndexUpserter>();
await using var sp = services.BuildServiceProvider();

var verb = args.FirstOrDefault() ?? "help";
switch (verb)
{
    case "index-create":
    {
        var up = sp.GetRequiredService<IndexUpserter>();
        await up.EnsureIndexAsync(SearchIndexes.SocialMap);
        return 0;
    }
    case "ingest-social-map":
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(180) };
        var blobs = sp.GetRequiredService<BlobServiceClient>().GetBlobContainerClient("snapshots");
        var day = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var geocoder = new PdokGeocoder(http);
        var records = new List<SocialMapRecord>();

        // 1. Samenwerkende Catalogi (CC0) + gemeentepagina's (alleen waar toegestaan — ADR-0002)
        var sc = new SamenwerkendeCatalogiSource(http);
        var fetcher = new PageFetcher(http);
        foreach (var (authority, municipality) in new[] { ("'s-Gravenhage", "Den Haag"), ("Zoetermeer", "Zoetermeer") })
        {
            var page = await sc.FetchAllAsync(authority, municipality);
            var centroid = await geocoder.MunicipalityCentroidAsync(municipality);
            var withText = 0;
            for (var i = 0; i < page.Count; i++)
            {
                var fetched = await fetcher.FetchAsync(new Uri(page[i].SourceUrl));
                if (fetched is not null) withText++;
                var category = page[i].Category ?? (fetched?.Keywords is { } kw ? SocialeKaartRag.Core.SocialMap.Taxonomy.FromSamenwerkendeCatalogi(null, page[i].Name, kw) : null);
                page[i] = page[i] with { BodyText = fetched?.Text, Category = category, Lat = centroid?.Lat, Lon = centroid?.Lon };
            }
            Console.WriteLine($"sc {municipality}: {page.Count} producten, {withText} met paginatekst, {page.Count(p => p.Category is not null)} in taxonomie");
            records.AddRange(page);
        }
        await blobs.GetBlobClient($"social-map/{day}/sc.json").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(records)), overwrite: true);

        // 2. OpenStreetMap (ODbL)
        var (osm, osmSkipped) = await new OsmOverpassSource(http).FetchAllAsync();
        await blobs.GetBlobClient($"social-map/{day}/osm.json").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(osm)), overwrite: true);
        Console.WriteLine($"osm: {osm.Count} locaties ({osm.Count(r => r.Municipality == "Zoetermeer")} Zoetermeer), {osmSkipped} zonder naam/categorie overgeslagen");
        records.AddRange(osm);

        // 3. Chunk + upsert (records zonder categorie worden geteld, niet stilzwijgend weggelaten)
        var chunks = records.Select(SocialMapChunker.TryToChunk).OfType<SocialeKaartRag.Core.Chunks.Chunk>().ToList();
        Console.WriteLine($"chunks: {chunks.Count} ({records.Count - chunks.Count} zonder categorie overgeslagen)");
        var n = await sp.GetRequiredService<IndexUpserter>().UpsertAsync(SearchIndexes.SocialMap, chunks);
        Console.WriteLine($"klaar: {n} chunks in '{SearchIndexes.SocialMap}'");
        return 0;
    }
    case "query":
    {
        var text = string.Join(' ', args.Skip(1));
        var tool = new AzureSearchTool(sp.GetRequiredService<SearchIndexClient>(), sp.GetRequiredService<EmbeddingClient>(), SearchIndexes.SocialMap);
        foreach (var h in await tool.SearchAsync(new SearchQuery(text)))
            Console.WriteLine($"{h.Score:F4}  {h.SourceId}  [{h.Category}]  {h.HeadingPath}\n       {h.SourceUrl}\n       {h.Text[..Math.Min(120, h.Text.Length)].ReplaceLineEndings(" ")}");
        return 0;
    }
    default:
        Console.WriteLine("gebruik: index-create | ingest-social-map | query <tekst>");
        return 1;
}
