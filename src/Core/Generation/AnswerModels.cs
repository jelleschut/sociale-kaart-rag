using System.Text.Json.Serialization;

namespace SocialeKaartRag.Core.Generation;

public sealed record AnswerItem(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("kind")] string Kind,            // "fact" | "summary"
    [property: JsonPropertyName("citations")] string[] Citations); // chunk-ids

public sealed record Answer(
    [property: JsonPropertyName("answer")] AnswerItem[] Items,
    [property: JsonPropertyName("confidence")] string Confidence, // "high" | "low"
    [property: JsonPropertyName("followUp")] string? FollowUp);
