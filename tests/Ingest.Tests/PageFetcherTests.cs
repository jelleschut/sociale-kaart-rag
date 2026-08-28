using SocialeKaartRag.Ingest;

namespace SocialeKaartRag.Ingest.Tests;

public class PageFetcherTests
{
    private static string Fixture(string f) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", f));

    [Fact]
    public void Extracts_intro_paragraphs_and_keywords_from_zoetermeer_next_data()
    {
        var page = PageFetcher.ExtractZoetermeer(Fixture("zoetermeer-page.html"))!;
        Assert.Contains("ZoetermeerPas", page.Text);
        Assert.Contains("laag inkomen", page.Text);
        Assert.DoesNotContain("<", page.Text);
        Assert.Contains("tegemoetkoming", page.Keywords ?? "");
        Assert.True(page.Text.Length is > 200 and <= PageFetcher.MaxChars);
    }

    [Fact]
    public void Collects_processed_html_from_nested_paragraph_shapes()
    {
        var html = """
        <html><body><script id="__NEXT_DATA__" type="application/json">{"props":{"pageProps":{"contentDetails":{"data":{"route":{"nodeContext":{
          "title":"Test","fieldIntroduction":{"processed":"<p>Intro &amp; meer</p>"},
          "fieldParagraphs":[{"entity":{"fieldText":{"processed":"<p>Eerste <b>alinea</b></p>"}}},{"entity":{"fieldTitle":"Kosten","fieldItems":[{"entity":{"fieldText":{"processed":"<p>Gratis</p>"}}}]}}],
          "entityMetatags":[{"name":"keywords","value":"PDC, Z, kosten"}]}}}}}}}</script></body></html>
        """;
        var page = PageFetcher.ExtractZoetermeer(html)!;
        Assert.Equal("Test. Intro & meer Eerste alinea Kosten Gratis", page.Text);
        Assert.Equal("PDC, Z, kosten", page.Keywords);
    }

    [Fact]
    public void Returns_null_without_next_data()
        => Assert.Null(PageFetcher.ExtractZoetermeer("<html><body><main>plain</main></body></html>"));

    [Fact]
    public void Removes_personal_contact_details_but_keeps_organisation_lines()
    {
        var text = PageFetcher.RemovePersonalContacts("Bel Jan de Vries, 06-12345678, jan.devries@zoetermeer.nl. Algemeen: 14079, info@zoetermeer.nl");
        Assert.DoesNotContain("06-12345678", text);
        Assert.DoesNotContain("jan.devries@", text);
        Assert.Contains("14079", text);
        Assert.Contains("info@zoetermeer.nl", text);
    }

    [Fact]
    public void Related_content_widgets_inside_paragraphs_are_ignored()
    {
        var html = """
        <html><body><script id="__NEXT_DATA__" type="application/json">{"props":{"pageProps":{"contentDetails":{"data":{"route":{"nodeContext":{
          "title":"Test","fieldParagraphs":[
            {"entity":{"entityBundle":"text","fieldText":{"processed":"<p>Eigen tekst</p>"}}},
            {"entity":{"entityBundle":"related_content","__typename":"ParagraphRelatedContent","fieldItems":[{"entity":{"fieldTitle":"Ander product","fieldText":{"processed":"<p>Andere pagina</p>"}}}]}},
            {"entity":{"__typename":"ParagraphSearch","fieldTitle":"Zoeken","fieldText":{"processed":"<p>zoekwidget</p>"}}}
          ]}}}}}}}</script></body></html>
        """;
        var page = PageFetcher.ExtractZoetermeer(html)!;
        Assert.Equal("Test. Eigen tekst", page.Text);
    }

    [Theory]
    [InlineData("wmo.loket@zoetermeer.nl", true)]
    [InlineData("info.zoetermeer@zoetermeer.nl", true)]
    [InlineData("contact.wonen@zoetermeer.nl", true)]
    [InlineData("jan.devries@zoetermeer.nl", false)]
    [InlineData("j.devries@zoetermeer.nl", false)]
    public void Department_addresses_are_kept_personal_ones_removed(string email, bool kept)
        => Assert.Equal(kept, PageFetcher.RemovePersonalContacts("mail " + email).Contains(email));

    [Fact]
    public void Truncation_happens_on_a_word_boundary()
    {
        var word = "woord ";
        var html = "<html><body><script id=\"__NEXT_DATA__\" type=\"application/json\">{\"props\":{\"pageProps\":{\"contentDetails\":{\"data\":{\"route\":{\"nodeContext\":{\"title\":\"T\",\"fieldParagraphs\":[{\"entity\":{\"entityBundle\":\"text\",\"fieldText\":{\"processed\":\"<p>" + string.Concat(Enumerable.Repeat(word, 2000)) + "</p>\"}}}]}}}}}}}</script></body></html>";
        var page = PageFetcher.ExtractZoetermeer(html)!;
        Assert.True(page.Text.Length <= PageFetcher.MaxChars);
        Assert.EndsWith("woord", page.Text);
    }

    [Theory]
    [InlineData("https://www.zoetermeer.nl/zoetermeerpas", true)]
    [InlineData("https://www.zoetermeer.nl/x.pdf", false)]
    [InlineData("https://www.denhaag.nl/nl/x/", false)]     // ADR-0002: geen toestemming
    [InlineData("http://www.zoetermeer.nl/x", false)]        // alleen https
    [InlineData("https://evil.example/x", false)]
    public void Only_fetches_allowed_hosts(string url, bool allowed)
        => Assert.Equal(allowed, PageFetcher.IsAllowed(new Uri(url)));

    [Fact]
    public async Task Fetch_is_rate_limited_and_uses_own_user_agent()
    {
        var handler = new StubHandler();
        var f = new PageFetcher(new HttpClient(handler));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await f.FetchAsync(new Uri("https://www.zoetermeer.nl/a"));
        await f.FetchAsync(new Uri("https://www.zoetermeer.nl/b"));
        Assert.True(sw.ElapsedMilliseconds >= 590);
        Assert.All(handler.UserAgents, ua => Assert.Contains("sociale-kaart-rag", ua));
        Assert.Null(await f.FetchAsync(new Uri("https://www.denhaag.nl/nl/x/")));
        Assert.Equal(2, handler.UserAgents.Count);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public List<string> UserAgents { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            UserAgents.Add(request.Headers.UserAgent.ToString());
            return Task.FromResult(new HttpResponseMessage { Content = new StringContent("<html><body><script id=\"__NEXT_DATA__\" type=\"application/json\">{\"props\":{\"pageProps\":{\"contentDetails\":{\"data\":{\"route\":{\"nodeContext\":{\"title\":\"T\",\"fieldParagraphs\":[{\"entity\":{\"fieldText\":{\"processed\":\"<p>x</p>\"}}}]}}}}}}}</script></body></html>") });
        }
    }
}
