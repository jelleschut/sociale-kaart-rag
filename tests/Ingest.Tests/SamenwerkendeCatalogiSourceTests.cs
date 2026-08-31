using System.Net.Http;
using System.Threading;
using SocialeKaartRag.Ingest.Sources;

namespace SocialeKaartRag.Ingest.Tests;

public class SamenwerkendeCatalogiSourceTests
{
    private static string Fixture() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sc-sample.xml"));

    [Fact]
    public void Parses_sru_records_into_social_map_records()
    {
        var (records, total, _) = SamenwerkendeCatalogiSource.Parse(Fixture(), "Den Haag");
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
        Assert.Equal(r.Category, r.Categories.FirstOrDefault()); // primaire categorie = eerste van de set
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
        var (records, total, skipped) = SamenwerkendeCatalogiSource.Parse(xml, "Zoetermeer");
        Assert.Equal(2, total);
        var r = Assert.Single(records);
        Assert.Equal("Bijstand aanvragen", r.Name);
        Assert.Equal("sc:https://www.zoetermeer.nl/x", r.SourceId); // productID ontbreekt → url
        Assert.Equal(1, skipped);
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

    private sealed class StubHandler(Func<Uri, string> respond) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Requests.Add(request.RequestUri!); return Task.FromResult(new HttpResponseMessage { Content = new StringContent(respond(request.RequestUri!)) }); }
    }

    private static string Page(int total, params string[] titles)
    {
        var recs = string.Join("", titles.Select(t => $"""<record><recordData><overheidproduct:scproduct xmlns:overheidproduct="http://standaarden.overheid.nl/product/terms/" xmlns:dcterms="http://purl.org/dc/terms/"><dcterms:identifier><![CDATA[https://www.zoetermeer.nl/{t}]]></dcterms:identifier><dcterms:title><![CDATA[{t}]]></dcterms:title></overheidproduct:scproduct></recordData></record>"""));
        return $"""<searchRetrieveResponse xmlns="http://www.loc.gov/zing/srw/"><numberOfRecords>{total}</numberOfRecords><records>{recs}</records></searchRetrieveResponse>""";
    }

    [Fact]
    public async Task FetchAll_pages_until_total_and_counts_skipped()
    {
        // total 3 met pagina's van 50 → 1 request; simuleer PageSize-gedrag met een kleine total en 2 pagina's door total=51
        var handler = new StubHandler(u => u.Query.Contains("startRecord=1") ? Page(51, "a", "b") : Page(51, "c"));
        var src = new SamenwerkendeCatalogiSource(new HttpClient(handler));
        var all = await src.FetchAllAsync("Zoetermeer", "Zoetermeer");
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(["a", "b", "c"], all.Select(r => r.Name));
    }

    [Fact]
    public async Task FetchAll_stops_on_unexpected_empty_page()
    {
        var handler = new StubHandler(u => u.Query.Contains("startRecord=1") ? Page(120, "a") : Page(120));
        var all = await new SamenwerkendeCatalogiSource(new HttpClient(handler)).FetchAllAsync("Zoetermeer", "Zoetermeer");
        Assert.Single(all);
        Assert.Equal(2, handler.Requests.Count); // tweede pagina leeg → stoppen, geen derde request
    }
}
