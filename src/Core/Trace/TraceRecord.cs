using System.Text.Json;
using System.Text.Json.Serialization;

namespace SocialeKaartRag.Core.Trace;

public enum TraceOutcome { Answered, RefusedMedical, RefusedScope, Escalated, Error }

public sealed record ToolCall(string Name, string ArgumentsHash, int ResultCount);

/// <summary>Eén record per request (spec §4.5). Bevat nooit vraag- of antwoordtekst; wel hashes.</summary>
public sealed record TraceRecord
{
    public required string CorrelationId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public string PolicyVersion { get; init; } = Policy.PolicyVersion.Current;
    public string? Model { get; init; }
    public string? ModelVersion { get; init; }
    public string? PromptHash { get; init; }
    public bool PiiRedacted { get; init; }
    public string[] PiiTypes { get; init; } = [];
    public string? Intent { get; init; }
    public string? Domain { get; init; }
    public ToolCall[] ToolCalls { get; init; } = [];
    public string[] RetrievedChunkIds { get; init; } = [];
    public double[] RetrievedScores { get; init; } = [];
    public int TokensIn { get; init; }
    public int TokensOut { get; init; }
    public int TokensCached { get; init; }
    public double EstimatedCostEur { get; init; }
    public long LatencyMs { get; init; }
    public TraceOutcome Outcome { get; init; } = TraceOutcome.Error;
    public string? RefusalReason { get; init; }

    public static TraceRecord Start(string correlationId) => new() { CorrelationId = correlationId, Timestamp = DateTimeOffset.UtcNow };

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
