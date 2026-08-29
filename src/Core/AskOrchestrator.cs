using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SocialeKaartRag.Core.Generation;
using SocialeKaartRag.Core.Policy;
using SocialeKaartRag.Core.Retrieval;
using SocialeKaartRag.Core.Trace;

namespace SocialeKaartRag.Core;

public sealed record SourceRef(string Id, string SourceId, string? Url, string? Heading, string? LastVerified, string? Attribution = null);

public sealed record AskResult(
    string CorrelationId, TraceOutcome Outcome, string? Message, Answer? Answer, IReadOnlyList<SourceRef> Sources, string PolicyVersion);

/// <summary>De gateway-laag in code (spec §4.3): PII → intent → allow-listed tool → generatie → citatiefilter → trace.</summary>
public sealed class AskOrchestrator(
    IIntentClassifier classifier,
    IEnumerable<ISearchTool> tools,
    IAnswerGenerator generator,
    ITraceSink trace,
    IGeocoder geocoder) : IAskOrchestrator
{
    private readonly Dictionary<string, ISearchTool> _tools = tools.ToDictionary(t => t.Corpus);

    // De zes sociale-kaart-domeinen (spec §4.2); out_of_scope levert geen categorie-boost op.
    private static readonly HashSet<string> SocialMapCategories =
        ["gezondheid", "werk_inkomen", "wonen", "vervoer", "welzijn", "mantelzorg"];

    private static string? DomainToCategory(string domain) => SocialMapCategories.Contains(domain) ? domain : null;

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

            GeoPoint? near = null;
            if (tool.Corpus == SearchIndexes.SocialMapCorpus && PostcodeDetector.Find(pii.Text) is { } pc)
            {
                // Een falende geocoder degradeert naar geen geo-filter i.p.v. de hele aanvraag te laten mislukken.
                try { near = await geocoder.PostcodeAsync(pc, ct); }
                catch (Exception) { near = null; }
            }
            // De categorie uit de intent is een voorkeur, geen grens: ze gaat als scoring-boost mee de zoektool in
            // (ADR-0006), niet als filter. Daarom precies één search-call — de retry "opnieuw zonder categorie"
            // is vervallen; er is niets meer om weg te laten.
            var category = tool.Corpus == SearchIndexes.SocialMapCorpus ? DomainToCategory(intent.Domain) : null;
            var query = new SearchQuery(pii.Text, category, near);
            var hits = await tool.SearchAsync(query, ct);
            var call = new ToolCall(tool.Name, Sha256($"{query.Text}|{query.Category}|{query.Near?.Lat}|{query.Near?.Lon}|{query.TopK}"), hits.Count);
            record = record with
            {
                ToolCalls = [call],
                RetrievedChunkIds = hits.Select(h => h.Id).ToArray(),
                RetrievedScores = hits.Select(h => h.Score).ToArray(),
            };
            var sources = hits.Select(h => new SourceRef(h.Id, h.SourceId, h.SourceUrl, h.HeadingPath, h.LastVerified, h.Attribution)).ToList();

            // 5a. Escalatie op retrieval-score
            if (hits.Count == 0 || hits.Max(h => h.Score) < PolicyVersion.EscalationScoreThreshold)
                return await Finish(record, TraceOutcome.Escalated, "low_retrieval_score", RefusalTexts.Escalated, null, [], sw);

            // 4. Generatie (bronnen als untrusted content in de user-turn)
            var gen = await generator.GenerateAsync(pii.Text, hits, ct);
            var (modelName, modelVersion) = SplitModel(gen.Usage.Model);
            record = record with
            {
                Model = modelName, ModelVersion = modelVersion, PromptHash = gen.PromptHash,
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
            await WriteTrace(record with { Outcome = TraceOutcome.Error, LatencyMs = sw.ElapsedMilliseconds });
            throw;
        }
    }

    private async Task<AskResult> Finish(TraceRecord record, TraceOutcome outcome, string? reason, string? message, Answer? answer, List<SourceRef> sources, Stopwatch sw)
    {
        await WriteTrace(record with { Outcome = outcome, RefusalReason = reason, LatencyMs = sw.ElapsedMilliseconds });
        return new AskResult(record.CorrelationId, outcome, message, answer, sources, record.PolicyVersion);
    }

    /// <summary>Precies één trace per request; een falende sink mag het antwoord niet blokkeren en geen tweede
    /// (error-)trace uitlokken. CompositeTraceSink logt sink-fouten zelf; dit is de laatste vangrail.</summary>
    private async Task WriteTrace(TraceRecord record)
    {
        try { await trace.WriteAsync(record, CancellationToken.None); }
        catch (Exception) { /* bewust: trace-fout is nooit fataal voor de aanvraag */ }
    }

    private static string Sha256(string s) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

    /// <summary>Splitst een modelstring als "gpt-4.1-mini-2025-04-14" in naam en release-datum; zonder
    /// zo'n datumsuffix blijft de naam ongewijzigd en is de versie null.</summary>
    private static readonly Regex ModelVersionSuffix = new(@"^(?<name>.+)-(?<version>\d{4}-\d{2}-\d{2})$", RegexOptions.Compiled);

    public static (string Name, string? Version) SplitModel(string model)
    {
        var m = ModelVersionSuffix.Match(model);
        return m.Success ? (m.Groups["name"].Value, m.Groups["version"].Value) : (model, null);
    }
}
