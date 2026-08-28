using System.Text.Json;
using SocialeKaartRag.Core.Trace;

namespace SocialeKaartRag.Core.Tests;

public class TraceRecordTests
{
    [Fact]
    public void Serializes_with_snake_case_outcome_and_without_question_or_answer_text()
    {
        var t = TraceRecord.Start("corr-1") with
        {
            Intent = "find_help", Domain = "wonen", PiiRedacted = true, PiiTypes = ["bsn"],
            Outcome = TraceOutcome.RefusedMedical, RetrievedChunkIds = ["c1"], RetrievedScores = [0.9],
            ToolCalls = [new ToolCall("search_social_map", "abc", 1)],
        };
        var json = JsonSerializer.Serialize(t, TraceRecord.JsonOptions);

        Assert.Contains("\"correlationId\":\"corr-1\"", json);
        Assert.Contains("\"policyVersion\":\"1.0.1\"", json);
        Assert.Contains("\"outcome\":\"refused_medical\"", json);
        Assert.Contains("\"piiTypes\":[\"bsn\"]", json);
        Assert.DoesNotContain("question", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("answerText", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"model\"", json); // null weggelaten
    }

    [Fact]
    public void Roundtrips_through_json()
    {
        var t = TraceRecord.Start("x") with { Outcome = TraceOutcome.Escalated, RefusalReason = "low_retrieval_score" };
        var back = JsonSerializer.Deserialize<TraceRecord>(JsonSerializer.Serialize(t, TraceRecord.JsonOptions), TraceRecord.JsonOptions)!;
        Assert.Equal(TraceOutcome.Escalated, back.Outcome);
        Assert.Equal("low_retrieval_score", back.RefusalReason);
    }

    [Theory]
    [InlineData("gpt-4.1-mini", 1000, 500, 0, 0.001104)]      // $0.40/M in, $1.60/M out → €-koers 0,92
    [InlineData("gpt-4.1-mini", 1000, 0, 1000, 0.000184)]     // volledig gecachte input: 50 %
    [InlineData("gpt-4.1-mini-2025-04-14", 1000, 500, 0, 0.001104)] // versie-suffix
    [InlineData("text-embedding-3-small", 1000000, 0, 0, 0.0184)]
    [InlineData("unknown-model", 1000, 500, 0, 0.001104)]     // valt terug op chat-tarief
    public void Estimates_cost_in_eur(string model, int tokensIn, int tokensOut, int cached, double expectedEur)
        => Assert.Equal(expectedEur, CostEstimator.EstimateEur(model, tokensIn, tokensOut, cached), 6);

    [Fact]
    public async Task Composite_sink_keeps_going_when_one_sink_throws()
    {
        var ok = new MemorySink();
        var composite = new CompositeTraceSink([new ThrowingSink(), ok], Microsoft.Extensions.Logging.Abstractions.NullLogger<CompositeTraceSink>.Instance);
        await composite.WriteAsync(TraceRecord.Start("c"));
        Assert.Equal("c", ok.Last!.CorrelationId);
    }

    private sealed class ThrowingSink : ITraceSink
    {
        public Task WriteAsync(TraceRecord r, CancellationToken ct = default) => throw new InvalidOperationException("boom");
    }
    private sealed class MemorySink : ITraceSink
    {
        public TraceRecord? Last;
        public Task WriteAsync(TraceRecord r, CancellationToken ct = default) { Last = r; return Task.CompletedTask; }
    }
}
