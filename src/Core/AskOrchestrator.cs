using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using SocialeKaartRag.Core.Generation;
using SocialeKaartRag.Core.Policy;
using SocialeKaartRag.Core.Retrieval;
using SocialeKaartRag.Core.Trace;

namespace SocialeKaartRag.Core;

public sealed record SourceRef(string Id, string SourceId, string? Url, string? Heading, string? LastVerified);

public sealed record AskResult(
    string CorrelationId, TraceOutcome Outcome, string? Message, Answer? Answer, IReadOnlyList<SourceRef> Sources, string PolicyVersion);

/// <summary>De gateway-laag in code (spec §4.3): PII → intent → allow-listed tool → generatie → citatiefilter → trace.</summary>
public sealed class AskOrchestrator(
    IIntentClassifier classifier,
    IEnumerable<ISearchTool> tools,
    IAnswerGenerator generator,
    ITraceSink trace)
{
    private readonly Dictionary<string, ISearchTool> _tools = tools.ToDictionary(t => t.Corpus);

    public async Task<AskResult> AskAsync(string question, string correlationId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var record = TraceRecord.Start(correlationId);
        try
        {
            // 1. PII — alleen typen worden gelogd, nooit waarden.
            var pii = PiiFilter.Redact(question);
            record = record with { PiiRedacted = pii.Redacted, PiiTypes = pii.Types.ToArray() };

            // 2. Intent
            var intent = await classifier.ClassifyAsync(pii.Text, ct);
            record = record with { Intent = intent.Kind, Domain = intent.Domain };
            if (intent.IsMedical) return await Finish(record, TraceOutcome.RefusedMedical, "medical_advice", RefusalTexts.Medical, null, [], sw);
            if (intent.IsOutOfScope) return await Finish(record, TraceOutcome.RefusedScope, "out_of_scope", RefusalTexts.OutOfScope, null, [], sw);

            // 3. Tool allow-list: alleen de geregistreerde tool voor het gekozen corpus; de orchestrator roept aan, niet het model.
            if (!_tools.TryGetValue(intent.Corpus, out var tool))
                return await Finish(record, TraceOutcome.RefusedScope, "no_tool_for_corpus", RefusalTexts.OutOfScope, null, [], sw);
            var query = new SearchQuery(pii.Text);
            var hits = await tool.SearchAsync(query, ct);
            record = record with
            {
                ToolCalls = [new ToolCall(tool.Name, Sha256(query.Text + "|" + query.TopK), hits.Count)],
                RetrievedChunkIds = hits.Select(h => h.Id).ToArray(),
                RetrievedScores = hits.Select(h => h.Score).ToArray(),
            };
            var sources = hits.Select(h => new SourceRef(h.Id, h.SourceId, h.SourceUrl, h.HeadingPath, h.LastVerified)).ToList();

            // 5a. Escalatie op retrieval-score
            if (hits.Count == 0 || hits.Max(h => h.Score) < PolicyVersion.EscalationScoreThreshold)
                return await Finish(record, TraceOutcome.Escalated, "low_retrieval_score", RefusalTexts.Escalated, null, [], sw);

            // 4. Generatie (bronnen als untrusted content in de user-turn)
            var gen = await generator.GenerateAsync(pii.Text, hits, ct);
            record = record with
            {
                Model = gen.Usage.Model, PromptHash = gen.PromptHash,
                TokensIn = gen.Usage.TokensIn, TokensOut = gen.Usage.TokensOut, TokensCached = gen.Usage.TokensCached,
                EstimatedCostEur = CostEstimator.EstimateEur(gen.Usage.Model, gen.Usage.TokensIn, gen.Usage.TokensOut, gen.Usage.TokensCached),
            };

            // 5b. Citatiefilter; niets over → escalatie
            var answer = CitationFilter.Apply(gen.Answer, hits.Select(h => h.Id).ToHashSet());
            if (answer.Items.Length == 0)
                return await Finish(record, TraceOutcome.Escalated, "no_cited_answer", RefusalTexts.Escalated, null, [], sw);

            var cited = answer.Items.SelectMany(i => i.Citations).ToHashSet();
            return await Finish(record, TraceOutcome.Answered, null, null, answer, sources.Where(s => cited.Contains(s.Id)).ToList(), sw);
        }
        catch (Exception)
        {
            await trace.WriteAsync(record with { Outcome = TraceOutcome.Error, LatencyMs = sw.ElapsedMilliseconds }, CancellationToken.None);
            throw;
        }
    }

    private async Task<AskResult> Finish(TraceRecord record, TraceOutcome outcome, string? reason, string? message, Answer? answer, List<SourceRef> sources, Stopwatch sw)
    {
        await trace.WriteAsync(record with { Outcome = outcome, RefusalReason = reason, LatencyMs = sw.ElapsedMilliseconds }, CancellationToken.None);
        return new AskResult(record.CorrelationId, outcome, message, answer, sources, record.PolicyVersion);
    }

    private static string Sha256(string s) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
}
