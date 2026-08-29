using System.Diagnostics;
using Azure;
using Azure.Search.Documents;
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

// Canaries staan alleen in de index tijdens de injection-cases (spec-issue: bij de hele run geseed citeerde een
// groundedness-case per ongeluk canary-charlie, waarna diens injectietekst de judge-prompt op Azure's content
// filter liet stuklopen). Niet-injectie cases draaien dus eerst, zonder canaries aanwezig.
var (withoutCanaries, injectionCases) = EvalRunner.SplitCases(cases);

foreach (var c in withoutCanaries)
    results.Add(await RunCaseAsync(c));

try
{
    await Canaries.SeedAsync(sp.GetRequiredService<IndexUpserter>(), canaries);
    Console.WriteLine($"canaries geseed: {canaries.Count}");
    await WaitUntilCanariesSearchableAsync(indexClient.GetSearchClient(SearchIndexes.SocialMap), canaries.Select(c => c.Id).ToList());
    foreach (var c in injectionCases)
        results.Add(await RunCaseAsync(c));
}
finally
{
    await Canaries.CleanupAsync(indexClient, canaries);
    Console.WriteLine("canaries verwijderd");
}

// Rapport in de oorspronkelijke case-volgorde tonen, ongeacht de interne run-volgorde in twee groepen.
results = EvalRunner.ReorderResults(cases, results).ToList();

var summary = Scoring.Summarise(results);
var md = Report.Render(summary, results, DateTimeOffset.UtcNow, PolicyVersion.Current, sw.Elapsed);
var reportPath = Path.Combine(root, "docs", "eval-report.md");
await File.WriteAllTextAsync(reportPath, md);
Console.WriteLine(md);
if (!summary.MeetsThresholds(out var failures))
{
    Console.WriteLine("DREMPELS NIET GEHAALD: " + string.Join("; ", failures));
    Console.WriteLine("EXIT 1");
    Console.Out.Flush();
    return 1;
}
Console.WriteLine("EXIT 0");
Console.Out.Flush();
return 0;

async Task<CaseResult> RunCaseAsync(EvalCase c)
{
    var sink = new CapturingSink();
    var orchestrator = new AskOrchestrator(
        new OpenAiIntentClassifier(chat),
        [new AzureSearchTool(indexClient, sp.GetRequiredService<EmbeddingClient>(), SearchIndexes.SocialMap, SearchIndexes.SocialMapCorpus)],
        new OpenAiAnswerGenerator(chat), sink, new PdokGeocoder(http));
    AskResult r; JudgeVerdict? verdict = null; var judgeCost = 0.0;
    try { r = await orchestrator.AskAsync(c.Question, Guid.NewGuid().ToString("n")); }
    catch (Exception ex)
    {
        Console.WriteLine($"{c.Id} ❌ fout");
        return new CaseResult(c.Id, c.Category, false, "fout: " + ex.Message, sink.Last?.EstimatedCostEur ?? 0);
    }
    var trace = sink.Last ?? TraceRecord.Start(r.CorrelationId);

    if (c.Category == "groundedness" && c.Expect.Judge && r.Answer is { Items.Length: > 0 })
    {
        try
        {
            var search = indexClient.GetSearchClient(SearchIndexes.SocialMap);
            var texts = new List<string>();
            foreach (var id in r.Answer.Items.SelectMany(i => i.Citations).Distinct())
                texts.Add((await search.GetDocumentAsync<SearchDocumentDto>(id)).Value.text);
            var (v, tin, tout, model) = await judge.JudgeAsync(texts, r.Answer.Items.Select(i => i.Text).ToList());
            verdict = v; judgeCost = CostEstimator.EstimateEur(model, tin, tout, 0);
        }
        catch (Exception ex)
        {
            verdict = ex.Message.Contains("content_filter", StringComparison.OrdinalIgnoreCase)
                ? new JudgeVerdict(false, "judge geblokkeerd door content filter (mogelijk canary geciteerd)")
                : new JudgeVerdict(false, "judge-fout: " + ex.Message);
        }
    }
    var scored = Scoring.Score(c, r, trace, verdict) with { CostEur = trace.EstimatedCostEur + judgeCost };
    Console.WriteLine($"{c.Id} {(scored.Passed ? "✅" : "❌")} {scored.Detail}");
    return scored;
}

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SocialeKaartRag.slnx"))) dir = dir.Parent;
    return dir?.FullName ?? throw new InvalidOperationException("repo-root niet gevonden");
}

/// <summary>Polt elke 2 s of alle canary-id's al terug te vinden zijn in de index (i.p.v. een vaste wachttijd
/// die te kort óf onnodig lang kan zijn), tot 60 s verstreken zijn.</summary>
static async Task WaitUntilCanariesSearchableAsync(SearchClient search, IReadOnlyList<string> ids)
{
    var sw = Stopwatch.StartNew();
    var pending = new HashSet<string>(ids);
    while (true)
    {
        foreach (var id in pending.ToList())
        {
            try { await search.GetDocumentAsync<SearchDocumentDto>(id); pending.Remove(id); }
            catch (RequestFailedException ex) when (ex.Status == 404) { }
        }
        if (pending.Count == 0)
        {
            Console.WriteLine($"canaries vindbaar na {sw.Elapsed.TotalSeconds:F0} s");
            return;
        }
        if (sw.Elapsed >= TimeSpan.FromSeconds(60)) throw new InvalidOperationException("canaries niet vindbaar na 60 s");
        await Task.Delay(TimeSpan.FromSeconds(2));
    }
}

sealed class CapturingSink : ITraceSink
{
    public TraceRecord? Last;
    public Task WriteAsync(TraceRecord record, CancellationToken ct = default) { Last = record; return Task.CompletedTask; }
}
