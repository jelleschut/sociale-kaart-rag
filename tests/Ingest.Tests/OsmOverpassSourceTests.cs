using SocialeKaartRag.Ingest.Sources;

namespace SocialeKaartRag.Ingest.Tests;

public class OsmOverpassSourceTests
{
    private static string Fixture() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "osm-sample.json"));

    [Fact]
    public void Maps_elements_to_records_with_coarse_geo_and_no_house_numbers()
    {
        var (records, _) = OsmOverpassSource.ParseWithReport(Fixture(), "Den Haag");
        Assert.NotEmpty(records);
        Assert.All(records, r =>
        {
            Assert.Equal("osm", r.Source);
            Assert.StartsWith("osm:", r.SourceId);
            Assert.StartsWith("https://www.openstreetmap.org/", r.SourceUrl);
            Assert.Contains("OpenStreetMap", r.Attribution);
            Assert.NotNull(r.Category);
            Assert.DoesNotMatch(@"\d", r.Street ?? "");
            Assert.Equal(Math.Round(r.Lat!.Value, 3), r.Lat);
            Assert.Equal(Math.Round(r.Lon!.Value, 3), r.Lon);
            Assert.Matches(@"^\d{4}[A-Z]{2}$", r.Postcode ?? "0000AA");
            Assert.Equal("Den Haag", r.Municipality);
        });
    }

    [Fact]
    public void Office_elements_are_excluded()
    {
        var (records, _) = OsmOverpassSource.ParseWithReport(Fixture(), "Den Haag");
        Assert.DoesNotContain(records, r => r.Name.Contains("Tribunal"));
    }

    [Fact]
    public void Elements_without_name_or_category_are_skipped_and_counted()
    {
        var json = """{"elements":[{"type":"node","id":1,"lat":52.1,"lon":4.3,"tags":{"amenity":"doctors"}},{"type":"node","id":2,"lat":52.1,"lon":4.3,"tags":{"name":"X","amenity":"cafe"}},{"type":"way","id":3,"center":{"lat":52.1234567,"lon":4.3456789},"tags":{"name":"Huisartsen Centrum","amenity":"doctors","addr:street":"Laan 12","addr:housenumber":"12","addr:postcode":"2511 cv","phone":"070-1234567","opening_hours":"Mo-Fr 08:00-17:00","social_facility:for":"senior;disabled"}}]}""";
        var (records, skipped) = OsmOverpassSource.ParseWithReport(json, "Den Haag");
        var r = Assert.Single(records);
        Assert.Equal(2, skipped);
        Assert.Equal("osm:way/3", r.SourceId);
        Assert.Equal("Laan", r.Street);
        Assert.Equal("2511CV", r.Postcode);
        Assert.Equal(52.123, r.Lat); Assert.Equal(4.346, r.Lon);
        Assert.Equal("huisartsenpraktijk", r.Summary);
        Assert.Equal("gezondheid", r.Category);
        Assert.Equal(["gezondheid"], r.Categories);
    }

    [Fact]
    public void Nursing_home_carries_both_wonen_and_gezondheid()
    {
        var json = """{"elements":[{"type":"node","id":7,"lat":52.1,"lon":4.3,"tags":{"name":"Verpleeghuis De Linde","amenity":"social_facility","social_facility":"nursing_home"}}]}""";
        var r = Assert.Single(OsmOverpassSource.Parse(json, "Den Haag"));
        Assert.Equal("wonen", r.Category);
        Assert.Equal(["wonen", "gezondheid"], r.Categories);
    }

    [Fact]
    public void All_records_are_labelled_with_the_given_municipality()
    {
        var (records, _) = OsmOverpassSource.ParseWithReport(Fixture(), "Zoetermeer");
        Assert.NotEmpty(records);
        Assert.All(records, r => Assert.Equal("Zoetermeer", r.Municipality));
    }

    [Fact]
    public void QueryFor_scopes_to_one_municipality_without_office()
    {
        var query = OsmOverpassSource.QueryFor("Zoetermeer");
        Assert.Contains("\"Zoetermeer\"", query);
        Assert.Contains("out tags center", query);
        Assert.DoesNotContain("office", query);
    }

    [Fact]
    public void Description_tag_goes_through_pii_filter()
    {
        var json = """{"elements":[{"type":"node","id":1,"lat":52.1,"lon":4.3,"tags":{"name":"Buurthuis X","amenity":"community_centre","description":"Bel Piet: 06-12345678 of piet.jansen@x.nl"}}]}""";
        var (records, _) = OsmOverpassSource.ParseWithReport(json, "Den Haag");
        var r = Assert.Single(records);
        Assert.DoesNotContain("06-12345678", r.BodyText);
        Assert.DoesNotContain("piet.jansen@x.nl", r.BodyText);
    }

    [Theory]
    [InlineData("06-12345678", false)]
    [InlineData("+31 6 12345678", false)]
    [InlineData("070-1234567", true)]
    [InlineData("+31 79 316 4646", true)]
    public void Mobile_phone_numbers_are_redacted_landlines_kept(string phone, bool kept)
    {
        var json = $$$"""{"elements":[{"type":"node","id":9,"lat":52.1,"lon":4.3,"tags":{"name":"Praktijk X","amenity":"doctors","phone":"{{{phone}}}"}}]}""";
        var r = Assert.Single(OsmOverpassSource.Parse(json, "Den Haag"));
        if (kept) Assert.Equal(phone, r.Phone);
        else Assert.Null(r.Phone);
    }
}
