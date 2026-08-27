using System.Security.Cryptography;
using System.Text;

namespace SocialeKaartRag.Core.Chunks;

/// <summary>Eén indexeerbare eenheid met provenance (spec §4.1).</summary>
public sealed record Chunk
{
    public required string Id { get; init; }
    public required string Corpus { get; init; }        // "kb" | "social-map"
    public required string Source { get; init; }        // bv. "soevereinlab-knowledge"
    public required string SourceId { get; init; }      // bv. "topic-age#00"
    public string? SourceUrl { get; init; }
    public required DateTimeOffset RetrievedAt { get; init; }
    public required string ContentHash { get; init; }
    public string? Category { get; init; }
    public string? LastVerified { get; init; }
    public string? HeadingPath { get; init; }
    public string[] Tags { get; init; } = [];
    public double? Lat { get; init; }
    public double? Lon { get; init; }
    public required string Text { get; init; }

    public static string MakeId(string corpus, string sourceId) => Sha1($"{corpus}|{sourceId}");
    public static string HashContent(string text) => Sha1(text);

    private static string Sha1(string s) =>
        Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(s)));
}
