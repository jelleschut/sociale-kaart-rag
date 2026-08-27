using System.Text.Json;
using System.Text.Json.Serialization;
using SocialeKaartRag.Core.Chunks;

namespace SocialeKaartRag.Ingest.Sources;

public sealed record IngestReport(IReadOnlyList<Chunk> Valid, int InvalidCount, IReadOnlyList<string> Errors);

/// <summary>Leest kb-chunks.jsonl (export van soevereinlab-knowledge); één record = één chunk, ongewijzigd (spec §2).
/// Ongeldige regels worden geteld en gerapporteerd, nooit stilzwijgend overgeslagen (spec §4.1).</summary>
public static class KbChunksSource
{
    public const string SourceName = "soevereinlab-knowledge";
    private const string RepoBlobBase = "https://gitlab.com/platform-engineer9761628/soevereine-cloud/soevereinlab-knowledge/-/blob/main";

    private sealed record Record(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("chunk_id")] string? ChunkId,
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("heading_path")] string[]? HeadingPath,
        [property: JsonPropertyName("tags")] string[]? Tags,
        [property: JsonPropertyName("last_verified")] string? LastVerified,
        [property: JsonPropertyName("text")] string? Text);

    public static async Task<IngestReport> ReadWithReportAsync(string path, CancellationToken ct = default)
    {
        var valid = new List<Chunk>();
        var errors = new List<string>();
        var retrievedAt = DateTimeOffset.UtcNow;
        var lineNo = 0;

        await foreach (var line in File.ReadLinesAsync(path, ct))
        {
            lineNo++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            Record? r;
            try { r = JsonSerializer.Deserialize<Record>(line); }
            catch (JsonException ex) { errors.Add($"regel {lineNo}: ongeldige JSON ({ex.Message})"); continue; }

            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(r?.Id)) missing.Add("id");
            if (string.IsNullOrWhiteSpace(r?.ChunkId)) missing.Add("chunk_id");
            if (string.IsNullOrWhiteSpace(r?.Text)) missing.Add("text");
            if (missing.Count > 0) { errors.Add($"regel {lineNo}: verplichte velden ontbreken: {string.Join(", ", missing)}"); continue; }

            valid.Add(new Chunk
            {
                Id = Chunk.MakeId("kb", r!.ChunkId!),
                Corpus = "kb",
                Source = SourceName,
                SourceId = r.ChunkId!,
                SourceUrl = SourceUrlFor(r.Type, r.Id!),
                RetrievedAt = retrievedAt,
                ContentHash = Chunk.HashContent(r.Text!),
                Category = r.Type,
                LastVerified = r.LastVerified,
                HeadingPath = r.HeadingPath is { Length: > 0 } hp ? string.Join(" > ", hp) : null,
                Tags = r.Tags ?? [],
                Text = r.Text!,
            });
        }
        return new IngestReport(valid, errors.Count, errors);
    }

    /// <summary>Bestandsnaam in soevereinlab-knowledge is altijd `&lt;id&gt;.md` in de map van het type
    /// (topics/topic-age.md, runbooks/rb-…md, decisions/dec-…md, reference/…md).</summary>
    public static string SourceUrlFor(string? type, string id)
    {
        var folder = type switch
        {
            "topic" => "topics", "runbook" => "runbooks", "decision" => "decisions", "reference" => "reference",
            _ => "docs",
        };
        return $"{RepoBlobBase}/{folder}/{id}.md";
    }
}
