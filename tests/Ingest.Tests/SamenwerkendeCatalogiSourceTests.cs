using SocialeKaartRag.Ingest.Sources;

namespace SocialeKaartRag.Ingest.Tests;

public class SamenwerkendeCatalogiSourceTests
{
    private static string Fixture() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sc-sample.xml"));

    [Fact]
    public void Parses_sru_records_into_social_map_records()
    {
        var (records, total) = SamenwerkendeCatalogiSource.Parse(Fixture(), "Den Haag");
        Assert.True(total >= 1);
        Assert.NotEmpty(records);
        var r = records[0];
        Assert.Equal("sc", r.Source);
        Assert.StartsWith("sc:", r.SourceId);
        Assert.StartsWith("https://www.denhaag.nl/", r.SourceUrl);
        Assert.False(string.IsNullOrWhiteSpace(r.Name));
        Assert.False(string.IsNullOrWhiteSpace(r.Summary));
        Assert.Equal("Den Haag", r.Municipality);
        Assert.Contains("Samenwerkende Catalogi", r.Attribution);
        Assert.Contains("CC0", r.Attribution);
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", r.LastModified!);
        Assert.Equal("werk_inkomen", r.Category); // bijzondere bijstand
    }

    [Fact]
    public void Record_without_title_or_url_is_skipped_and_counted()
    {
        var xml = """
        <searchRetrieveResponse xmlns="http://www.loc.gov/zing/srw/"><numberOfRecords>2</numberOfRecords><records>
        <record><recordData><overheidproduct:scproduct xmlns:overheidproduct="http://standaarden.overheid.nl/product/terms/" xmlns:dcterms="http://purl.org/dc/terms/">
          <dcterms:title><![CDATA[Zonder url]]></dcterms:title></overheidproduct:scproduct></recordData></record>
        <record><recordData><overheidproduct:scproduct xmlns:overheidproduct="http://standaarden.overheid.nl/product/terms/" xmlns:dcterms="http://purl.org/dc/terms/">
          <dcterms:identifier><![CDATA[https://www.zoetermeer.nl/x]]></dcterms:identifier><dcterms:title><![CDATA[Bijstand aanvragen]]></dcterms:title><dcterms:abstract><![CDATA[Uitkering]]></dcterms:abstract><dcterms:modified>2026-07-01</dcterms:modified></overheidproduct:scproduct></recordData></record>
        </records></searchRetrieveResponse>
        """;
        var (records, total) = SamenwerkendeCatalogiSource.Parse(xml, "Zoetermeer");
        Assert.Equal(2, total);
        var r = Assert.Single(records);
        Assert.Equal("Bijstand aanvragen", r.Name);
        Assert.Equal("sc:https://www.zoetermeer.nl/x", r.SourceId); // productID ontbreekt → url
        Assert.Equal(1, SamenwerkendeCatalogiSource.LastSkipped);
    }

    [Fact]
    public void Builds_paged_query_urls()
    {
        var u = SamenwerkendeCatalogiSource.QueryUrl("'s-Gravenhage", startRecord: 51, max: 50);
        Assert.Contains("x-connection=sc", u);
        Assert.Contains("startRecord=51", u);
        Assert.Contains("maximumRecords=50", u);
        Assert.Contains("authority%3D%22%27s-Gravenhage%22", u);
    }
}
