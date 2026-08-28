using System.Xml.Linq;
using SocialeKaartRag.Core.SocialMap;

namespace SocialeKaartRag.Ingest.Sources;

/// <summary>Samenwerkende Catalogi (overheid.nl) via SRU 1.2, x-connection=sc. Eén record = één gemeentelijk product.
/// Dataset "Producten en Diensten" is CC0 (data.overheid.nl, bevestigd 28-08-2026; ADR-0002).</summary>
public sealed class SamenwerkendeCatalogiSource(HttpClient http)
{
    public const string Endpoint = "https://zoekdienst.overheid.nl/sru/Search";
    private static readonly XNamespace Dcterms = "http://purl.org/dc/terms/";
    private static readonly XNamespace Product = "http://standaarden.overheid.nl/product/terms/";

    /// <summary>Aantal overgeslagen records (zonder titel/URL) van de laatste Parse-aanroep; ongeldige records worden geteld, niet verzwegen (spec §4.1).</summary>
    public static int LastSkipped;

    public static string QueryUrl(string authority, int startRecord, int max = 50) =>
        $"{Endpoint}?version=1.2&operation=searchRetrieve&x-connection=sc&maximumRecords={max}&startRecord={startRecord}" +
        $"&query={Uri.EscapeDataString($"authority=\"{authority}\"")}";

    public async Task<List<SocialMapRecord>> FetchAllAsync(string authority, string municipality, CancellationToken ct = default)
    {
        var all = new List<SocialMapRecord>(); var skipped = 0; var start = 1;
        while (true)
        {
            var xml = await http.GetStringAsync(QueryUrl(authority, start), ct);
            var (page, total) = Parse(xml, municipality);
            skipped += LastSkipped; all.AddRange(page); start += 50;
            if ((page.Count == 0 && LastSkipped == 0) || start > total) break;
        }
        if (skipped > 0) Console.WriteLine($"  ! sc {municipality}: {skipped} records zonder titel/URL overgeslagen");
        return all;
    }

    public static (List<SocialMapRecord> Records, int Total) Parse(string xml, string municipality)
    {
        var doc = XDocument.Parse(xml);
        var total = int.TryParse(doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "numberOfRecords")?.Value, out var t) ? t : 0;
        var records = new List<SocialMapRecord>(); LastSkipped = 0;
        foreach (var rec in doc.Descendants().Where(e => e.Name.LocalName == "recordData"))
        {
            string? V(XName n) => rec.Descendants(n).FirstOrDefault()?.Value.Trim();
            var title = V(Dcterms + "title"); var url = V(Dcterms + "identifier");
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url)) { LastSkipped++; continue; }
            var abstractText = V(Dcterms + "abstract") ?? "";
            var subject = V(Dcterms + "subject");
            var productId = V(Product + "productID");
            records.Add(new SocialMapRecord
            {
                Source = "sc", SourceId = "sc:" + (string.IsNullOrWhiteSpace(productId) ? url : productId), SourceUrl = url, Name = title, Summary = abstractText,
                Category = Taxonomy.FromSamenwerkendeCatalogi(subject, title, abstractText),
                Municipality = municipality, LastModified = V(Dcterms + "modified"),
                Attribution = $"Bron: gemeente {municipality} via Samenwerkende Catalogi (overheid.nl, CC0)",
            });
        }
        return (records, total);
    }
}
