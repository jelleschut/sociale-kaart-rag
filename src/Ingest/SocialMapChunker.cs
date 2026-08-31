using System.Text;
using SocialeKaartRag.Core.Chunks;
using SocialeKaartRag.Ingest.Sources;

namespace SocialeKaartRag.Ingest;

/// <summary>Sociale kaart = één chunk per organisatie-dienst (spec §4.1). Tekst is leesbaar Nederlands, zodat BM25 én embedding werken.
/// Attributie en gemeente reizen mee als tags ("attribution:…", "gemeente:…") — de index heeft daar geen apart veld voor.</summary>
public static class SocialMapChunker
{
    public static Chunk? TryToChunk(SocialMapRecord r) => r.Category is null ? null : ToChunk(r);

    public static Chunk ToChunk(SocialMapRecord r)
    {
        var sb = new StringBuilder();
        sb.Append(r.Name).Append(" (");
        if (r.Source == "osm" && !string.IsNullOrWhiteSpace(r.Summary)) sb.Append(r.Summary).Append(", ");
        sb.Append("gemeente ").Append(r.Municipality).AppendLine(")");
        if (r.Source != "osm" && !string.IsNullOrWhiteSpace(r.Summary)) sb.AppendLine(r.Summary);
        if (r.Street is not null || r.Postcode is not null)
            sb.Append("Adres: ").Append(string.Join(' ', new[] { r.Street, r.Postcode, r.Municipality }.Where(s => !string.IsNullOrWhiteSpace(s)))).AppendLine();
        if (r.Phone is not null) sb.Append("Telefoon: ").AppendLine(r.Phone);
        if (r.Website is not null) sb.Append("Website: ").AppendLine(r.Website);
        if (r.OpeningHours is not null) sb.Append("Openingstijden: ").AppendLine(r.OpeningHours);
        if (!string.IsNullOrWhiteSpace(r.BodyText)) sb.AppendLine().AppendLine(r.BodyText);
        var text = sb.ToString().Trim();
        return new Chunk
        {
            Id = Chunk.MakeId("social-map", r.SourceId), Corpus = "social-map", Source = r.Source, SourceId = r.SourceId, SourceUrl = r.SourceUrl,
            RetrievedAt = DateTimeOffset.UtcNow, ContentHash = Chunk.HashContent(text), Category = r.Category,
            Categories = r.Categories.Length > 0 ? r.Categories : r.Category is null ? [] : [r.Category], LastVerified = r.LastModified,
            HeadingPath = r.Name, Tags = ["gemeente:" + r.Municipality, "bron:" + r.Source, "attribution:" + r.Attribution], Lat = r.Lat, Lon = r.Lon, Text = text,
        };
    }
}
