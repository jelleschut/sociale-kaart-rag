using SocialeKaartRag.Core;
using SocialeKaartRag.Core.Generation;
using SocialeKaartRag.Core.Policy;
using SocialeKaartRag.Core.Retrieval;
using SocialeKaartRag.Core.Trace;

namespace SocialeKaartRag.Core.Tests;

public class AskOrchestratorTests
{
    private sealed class FakeClassifier(Intent intent) : IIntentClassifier
    {
        public Task<Intent> ClassifyAsync(string q, CancellationToken ct = default) => Task.FromResult(intent);
    }
    private sealed class FakeSearch(string corpus, params SearchHit[] hits) : ISearchTool
    {
        public string Name => "search_" + corpus.Replace('-', '_'); public string Corpus => corpus;
        public string? LastQuery;
        public Task<IReadOnlyList<SearchHit>> SearchAsync(SearchQuery q, CancellationToken ct = default) { LastQuery = q.Text; return Task.FromResult<IReadOnlyList<SearchHit>>(hits); }
    }
    private sealed class FakeGenerator(Answer answer) : IAnswerGenerator
    {
        public int Calls;
        public Task<GenerationResult> GenerateAsync(string q, IReadOnlyList<SearchHit> hits, CancellationToken ct = default)
        { Calls++; return Task.FromResult(new GenerationResult(answer, new GenerationUsage(100, 20, 0, "gpt-4.1-mini"), "hash")); }
    }
    private sealed class MemorySink : ITraceSink
    {
        public TraceRecord? Last;
        public Task WriteAsync(TraceRecord r, CancellationToken ct = default) { Last = r; return Task.CompletedTask; }
    }

    private static SearchHit Hit(string id, double score) => new(id, "kb", "s#" + id, "https://u/" + id, "topic", "H", null, "tekst " + id, score);
    private static Intent Kb => new("platform_kennis", "information", "kb");

    [Fact]
    public async Task Medical_intent_is_refused_without_retrieval_or_generation()
    {
        var search = new FakeSearch("kb", Hit("c1", 0.9)); var gen = new FakeGenerator(new Answer([], "low", null)); var sink = new MemorySink();
        var o = new AskOrchestrator(new FakeClassifier(new Intent("gezondheid", "medical_advice", "social-map")), [search], gen, sink);

        var r = await o.AskAsync("heb ik een longontsteking?", "corr");

        Assert.Equal(TraceOutcome.RefusedMedical, r.Outcome);
        Assert.Equal(RefusalTexts.Medical, r.Message);
        Assert.Null(search.LastQuery);
        Assert.Equal(0, gen.Calls);
        Assert.Equal(TraceOutcome.RefusedMedical, sink.Last!.Outcome);
        Assert.Equal("medical_advice", sink.Last.RefusalReason);
    }

    [Fact]
    public async Task Out_of_scope_is_refused()
    {
        var o = new AskOrchestrator(new FakeClassifier(new Intent("out_of_scope", "other", "social-map")), [new FakeSearch("kb")], new FakeGenerator(new Answer([], "low", null)), new MemorySink());
        var r = await o.AskAsync("wat is de hoofdstad van Peru?", "corr");
        Assert.Equal(TraceOutcome.RefusedScope, r.Outcome);
        Assert.Equal(RefusalTexts.OutOfScope, r.Message);
    }

    [Fact]
    public async Task Pii_is_redacted_before_search_and_only_types_are_traced()
    {
        var search = new FakeSearch("kb", Hit("c1", 0.9)); var sink = new MemorySink();
        var o = new AskOrchestrator(new FakeClassifier(Kb), [search], new FakeGenerator(new Answer([new("a", "fact", ["c1"])], "high", null)), sink);

        await o.AskAsync("mijn bsn is 111222333, hoe werkt sops?", "corr");

        Assert.DoesNotContain("111222333", search.LastQuery);
        Assert.True(sink.Last!.PiiRedacted);
        Assert.Equal(["bsn"], sink.Last.PiiTypes);
    }

    [Fact]
    public async Task Low_retrieval_score_escalates_without_generation()
    {
        var gen = new FakeGenerator(new Answer([new("a", "fact", ["c1"])], "high", null)); var sink = new MemorySink();
        var o = new AskOrchestrator(new FakeClassifier(Kb), [new FakeSearch("kb", Hit("c1", 0.001))], gen, sink);

        var r = await o.AskAsync("vraag", "corr");

        Assert.Equal(TraceOutcome.Escalated, r.Outcome);
        Assert.Equal(RefusalTexts.Escalated, r.Message);
        Assert.Equal(0, gen.Calls);
        Assert.Equal("low_retrieval_score", sink.Last!.RefusalReason);
    }

    [Fact]
    public async Task No_hits_escalates()
    {
        var o = new AskOrchestrator(new FakeClassifier(Kb), [new FakeSearch("kb")], new FakeGenerator(new Answer([], "low", null)), new MemorySink());
        Assert.Equal(TraceOutcome.Escalated, (await o.AskAsync("vraag", "corr")).Outcome);
    }

    [Fact]
    public async Task Answer_without_valid_citations_escalates()
    {
        var sink = new MemorySink();
        var o = new AskOrchestrator(new FakeClassifier(Kb), [new FakeSearch("kb", Hit("c1", 0.9))], new FakeGenerator(new Answer([new("verzonnen", "summary", ["c9"])], "high", null)), sink);
        var r = await o.AskAsync("vraag", "corr");
        Assert.Equal(TraceOutcome.Escalated, r.Outcome);
        Assert.Equal("no_cited_answer", sink.Last!.RefusalReason);
        Assert.Equal(20, sink.Last.TokensOut); // generatie vond wél plaats
    }

    [Fact]
    public async Task Unknown_corpus_is_refused_as_scope()
    {
        var o = new AskOrchestrator(new FakeClassifier(new Intent("wonen", "find_help", "social-map")), [new FakeSearch("kb")], new FakeGenerator(new Answer([], "low", null)), new MemorySink());
        Assert.Equal(TraceOutcome.RefusedScope, (await o.AskAsync("vraag", "corr")).Outcome);
    }

    [Fact]
    public async Task Happy_path_returns_cited_answer_and_full_trace()
    {
        var search = new FakeSearch("kb", Hit("c1", 0.9), Hit("c2", 0.5)); var sink = new MemorySink();
        var o = new AskOrchestrator(new FakeClassifier(Kb), [search], new FakeGenerator(new Answer([new("feit", "fact", ["c1"]), new("los", "summary", ["c9"])], "high", "meer?")), sink);

        var r = await o.AskAsync("hoe werkt sops?", "corr");

        Assert.Equal(TraceOutcome.Answered, r.Outcome);
        var item = Assert.Single(r.Answer!.Items);
        Assert.Equal("feit", item.Text);
        Assert.Equal("meer?", r.Answer.FollowUp);
        Assert.Equal("https://u/c1", Assert.Single(r.Sources).Url); // alleen geciteerde bronnen
        Assert.Equal(["c1", "c2"], sink.Last!.RetrievedChunkIds);
        Assert.Equal("search_kb", sink.Last.ToolCalls.Single().Name);
        Assert.Equal(2, sink.Last.ToolCalls.Single().ResultCount);
        Assert.True(sink.Last.EstimatedCostEur > 0);
        Assert.Equal("hash", sink.Last.PromptHash);
        Assert.Equal("corr", r.CorrelationId);
        Assert.Equal(PolicyVersion.Current, r.PolicyVersion);
    }

    [Fact]
    public async Task Exceptions_are_traced_as_error_and_rethrown()
    {
        var sink = new MemorySink();
        var o = new AskOrchestrator(new ThrowingClassifier(), [new FakeSearch("kb")], new FakeGenerator(new Answer([], "low", null)), sink);
        await Assert.ThrowsAsync<InvalidOperationException>(() => o.AskAsync("vraag", "corr"));
        Assert.Equal(TraceOutcome.Error, sink.Last!.Outcome);
    }
    private sealed class ThrowingClassifier : IIntentClassifier
    {
        public Task<Intent> ClassifyAsync(string q, CancellationToken ct = default) => throw new InvalidOperationException("boom");
    }
}
