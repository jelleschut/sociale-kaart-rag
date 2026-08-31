using SocialeKaartRag.Ingest;
using SocialeKaartRag.Ingest.Sources;

namespace SocialeKaartRag.Ingest.Tests;

public class SocialMapChunkerTests
{
    private static SocialMapRecord Osm() => new()
    {
        Source = "osm", SourceId = "osm:node/1", SourceUrl = "https://www.openstreetmap.org/node/1", Name = "Wijkcentrum De Regenvalk",
        Category = "welzijn", Categories = ["welzijn"], Summary = "wijkcentrum", Municipality = "Den Haag", Street = "Regentesseplein", Postcode = "2562EN",
        Phone = "+31 70 123 4567", Website = "https://example.org", OpeningHours = "Mo-Fr 09:00-17:00", Lat = 52.078, Lon = 4.286,
        Attribution = "© OpenStreetMap-bijdragers (ODbL 1.0)",
    };

    [Fact]
    public void Osm_record_becomes_one_chunk_with_contact_text_geo_and_attribution_tag()
    {
        var c = SocialMapChunker.ToChunk(Osm());
        Assert.Equal("social-map", c.Corpus);
        Assert.Equal("osm", c.Source);
        Assert.Equal("welzijn", c.Category);
        Assert.StartsWith("Wijkcentrum De Regenvalk (wijkcentrum, gemeente Den Haag)", c.Text);
        Assert.Contains("Adres: Regentesseplein 2562EN Den Haag", c.Text);
        Assert.Contains("Telefoon: +31 70 123 4567", c.Text);
        Assert.Contains("Openingstijden: Mo-Fr 09:00-17:00", c.Text);
        Assert.DoesNotMatch(@"Adres: [^\n]*\d{1,4}[a-z]?\s+2562EN", c.Text); // geen huisnummer vóór de postcode
        Assert.Equal(52.078, c.Lat); Assert.Equal(4.286, c.Lon);
        Assert.Contains("attribution:© OpenStreetMap-bijdragers (ODbL 1.0)", c.Tags);
        Assert.Contains("gemeente:Den Haag", c.Tags);
        Assert.Equal(SocialeKaartRag.Core.Chunks.Chunk.MakeId("social-map", "osm:node/1"), c.Id);
        Assert.Equal("Wijkcentrum De Regenvalk", c.HeadingPath);
    }

    [Fact]
    public void Sc_record_uses_title_summary_body_and_municipality_centroid()
    {
        var r = new SocialMapRecord
        {
            Source = "sc", SourceId = "sc:123", SourceUrl = "https://www.zoetermeer.nl/x", Name = "Bijzondere bijstand", Category = "werk_inkomen",
            Summary = "Extra geld bij hoge kosten.", BodyText = "Lange uitleg …", Municipality = "Zoetermeer", Lat = 52.061, Lon = 4.49, LastModified = "2026-07-01",
            Attribution = "Bron: gemeente Zoetermeer via Samenwerkende Catalogi (overheid.nl, CC0)",
        };
        var c = SocialMapChunker.ToChunk(r);
        Assert.StartsWith("Bijzondere bijstand (gemeente Zoetermeer)", c.Text);
        Assert.Contains("Extra geld", c.Text);
        Assert.Contains("Lange uitleg", c.Text);
        Assert.Equal("https://www.zoetermeer.nl/x", c.SourceUrl);
        Assert.Equal("2026-07-01", c.LastVerified);
        Assert.Equal(52.061, c.Lat);
    }

    [Fact]
    public void All_categories_travel_to_the_chunk_and_the_primary_is_the_first()
    {
        var r = new SocialMapRecord
        {
            Source = "sc", SourceId = "sc:zoetermeerpas", SourceUrl = "https://www.zoetermeer.nl/zoetermeerpas", Name = "ZoetermeerPas",
            Category = "werk_inkomen", Categories = ["werk_inkomen", "welzijn"], Summary = "Voordeelpas om mee te doen.",
            Municipality = "Zoetermeer", Attribution = "Bron: gemeente Zoetermeer via Samenwerkende Catalogi (overheid.nl, CC0)",
        };
        var c = SocialMapChunker.ToChunk(r);
        Assert.Equal("werk_inkomen", c.Category);
        Assert.Equal(["werk_inkomen", "welzijn"], c.Categories);
    }

    [Fact]
    public void Record_with_only_a_primary_category_still_gets_a_category_set()
        // Anders zou zo'n chunk zonder boostveld in de index belanden en de categorie-boost nooit kunnen raken.
        => Assert.Equal(["welzijn"], SocialMapChunker.ToChunk(Osm() with { Categories = [] }).Categories);

    [Fact]
    public void Records_without_category_are_rejected()
        => Assert.Null(SocialMapChunker.TryToChunk(Osm() with { Category = null, Categories = [] }));
}
