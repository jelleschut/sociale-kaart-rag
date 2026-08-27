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
        await up.EnsureIndexAsync(SearchIndexes.Kb);
        await up.EnsureIndexAsync(SearchIndexes.SocialMap);
        return 0;
    }
    case "ingest-kb":
    {
        var path = args.ElementAtOrDefault(1) ?? "kb-chunks.jsonl";
        var report = await KbChunksSource.ReadWithReportAsync(path);
        Console.WriteLine($"gelezen: {report.Valid.Count} geldig, {report.InvalidCount} ongeldig");
        foreach (var e in report.Errors) Console.WriteLine("  ! " + e);

        // Bron-snapshot naar Blob (spec §4.1: reproduceerbare ingestie).
        var blobs = sp.GetRequiredService<BlobServiceClient>().GetBlobContainerClient("snapshots");
        var snapshotName = $"kb/{DateTime.UtcNow:yyyy-MM-dd}/kb-chunks.jsonl";
        await blobs.GetBlobClient(snapshotName).UploadAsync(path, overwrite: true);
        Console.WriteLine($"snapshot: snapshots/{snapshotName}");

        var n = await sp.GetRequiredService<IndexUpserter>().UpsertAsync(SearchIndexes.Kb, report.Valid);
        Console.WriteLine($"klaar: {n} chunks in '{SearchIndexes.Kb}'");
        return report.InvalidCount > 0 ? 2 : 0;
    }
    case "query":
    {
        var text = string.Join(' ', args.Skip(1));
        var tool = new AzureSearchTool(sp.GetRequiredService<SearchIndexClient>(), sp.GetRequiredService<EmbeddingClient>(), SearchIndexes.Kb);
        foreach (var h in await tool.SearchAsync(new SearchQuery(text)))
            Console.WriteLine($"{h.Score:F4}  {h.SourceId}  [{h.Category}]  {h.HeadingPath}\n       {h.SourceUrl}\n       {h.Text[..Math.Min(120, h.Text.Length)].ReplaceLineEndings(" ")}");
        return 0;
    }
    default:
        Console.WriteLine("gebruik: index-create | ingest-kb [pad] | query <tekst>");
        return 1;
}
