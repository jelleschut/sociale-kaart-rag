using System.Diagnostics;
using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Chat;
using OpenAI.Embeddings;
using SocialeKaartRag.Core;
using SocialeKaartRag.Core.Generation;
using SocialeKaartRag.Core.Policy;
using SocialeKaartRag.Core.Retrieval;
using SocialeKaartRag.Core.Trace;
using SocialeKaartRag.Eval;
using SocialeKaartRag.Ingest;

var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
var services = new ServiceCollection().AddAzureClients(config);
services.AddSingleton<Embedder>(); services.AddSingleton<IndexUpserter>();
await using var sp = services.BuildServiceProvider();

var root = FindRepoRoot();
var cases = Cases.Load(Path.Combine(root, "eval", "cases.yaml"));
var canaries = Canaries.ToChunks(Canaries.Load(Path.Combine(root, "eval", "canaries.yaml")));
var indexClient = sp.GetRequiredService<SearchIndexClient>();
var chat = sp.GetRequiredService<ChatClient>();
var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
var judge = new OpenAiGroundednessJudge(chat);
var results = new List<CaseResult>();
var sw = Stopwatch.StartNew();

await Canaries.SeedAsync(sp.GetRequiredService<IndexUpserter>(), canaries);
Console.WriteLine($"canaries geseed: {canaries.Count}");
try
{
    await Task.Delay(TimeSpan.FromSeconds(5)); // indexering
    foreach (var c in cases)
    {
        var sink = new CapturingSink();
        var orchestrator = new AskOrchestrator(
            new OpenAiIntentClassifier(chat),
            [new AzureSearchTool(indexClient, sp.GetRequiredService<EmbeddingClient>(), SearchIndexes.SocialMap)],
            new OpenAiAnswerGenerator(chat), sink, new PdokGeocoder(http));
        AskResult r; JudgeVerdict? verdict = null; var judgeCost = 0.0;
        try { r = await orchestrator.AskAsync(c.Question, Guid.NewGuid().ToString("n")); }
        catch (Exception ex) { results.Add(new CaseResult(c.Id, c.Category, false, "fout: " + ex.Message, sink.Last?.EstimatedCostEur ?? 0)); Console.WriteLine($"{c.Id} ❌ fout"); continue; }
        var trace = sink.Last ?? TraceRecord.Start(r.CorrelationId);

        if (c.Category == "groundedness" && c.Expect.Judge && r.Answer is { Items.Length: > 0 })
        {
            var search = indexClient.GetSearchClient(SearchIndexes.SocialMap);
            var texts = new List<string>();
            foreach (var id in r.Answer.Items.SelectMany(i => i.Citations).Distinct())
                texts.Add((await search.GetDocumentAsync<SearchDocumentDto>(id)).Value.text);
            var (v, tin, tout) = await judge.JudgeAsync(texts, r.Answer.Items.Select(i => i.Text).ToList());
            verdict = v; judgeCost = CostEstimator.EstimateEur("gpt-4.1-mini", tin, tout, 0);
        }
        var scored = Scoring.Score(c, r, trace, verdict) with { CostEur = trace.EstimatedCostEur + judgeCost };
        results.Add(scored);
        Console.WriteLine($"{c.Id} {(scored.Passed ? "✅" : "❌")} {scored.Detail}");
    }
}
finally
{
    await Canaries.CleanupAsync(indexClient, canaries);
    Console.WriteLine("canaries verwijderd");
}

var summary = Scoring.Summarise(results);
var md = Report.Render(summary, results, DateTimeOffset.UtcNow, PolicyVersion.Current, sw.Elapsed);
var reportPath = Path.Combine(root, "docs", "eval-report.md");
await File.WriteAllTextAsync(reportPath, md);
Console.WriteLine(md);
if (!summary.MeetsThresholds(out var failures)) { Console.WriteLine("DREMPELS NIET GEHAALD: " + string.Join("; ", failures)); return 1; }
return 0;

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SocialeKaartRag.slnx"))) dir = dir.Parent;
    return dir?.FullName ?? throw new InvalidOperationException("repo-root niet gevonden");
}

sealed class CapturingSink : ITraceSink
{
    public TraceRecord? Last;
    public Task WriteAsync(TraceRecord record, CancellationToken ct = default) { Last = record; return Task.CompletedTask; }
}
