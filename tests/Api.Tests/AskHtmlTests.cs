using SocialeKaartRag.Api;
using SocialeKaartRag.Core;
using SocialeKaartRag.Core.Generation;
using SocialeKaartRag.Core.Trace;

namespace SocialeKaartRag.Api.Tests;

public class AskHtmlTests
{
    private static AskResult Answered() => new("abc123", TraceOutcome.Answered, null,
        new Answer([new("Feit <b>één</b>", "fact", ["c1"]), new("Samenvatting", "summary", ["c1", "c2"])], "high", "Wilt u meer weten?"),
        [new SourceRef("c1", "osm:node/1", "https://www.openstreetmap.org/node/1", "Wijkcentrum X", null, "© OpenStreetMap-bijdragers (ODbL 1.0)"),
         new SourceRef("c2", "sc:9", "https://www.zoetermeer.nl/x", "Regeling Y", "2026-07-01", "Bron: gemeente Zoetermeer via Samenwerkende Catalogi (overheid.nl, CC0)")],
        "1.0.1");

    [Fact]
    public void Renders_items_with_kind_badges_citation_numbers_and_escapes_html()
    {
        var html = AskHtml.Render(Answered());
        Assert.Contains("badge-feit", html);
        Assert.Contains("badge-samenvatting", html);
        Assert.Contains("Feit &lt;b&gt;één&lt;/b&gt;", html);
        Assert.DoesNotContain("<b>één</b>", html);
        Assert.Contains("<sup", html);
        Assert.Contains(">[1]<", html);
        Assert.Contains(">[2]<", html);
    }

    [Fact]
    public void Renders_sources_with_links_attribution_and_correlation_id()
    {
        var html = AskHtml.Render(Answered());
        Assert.Contains("href=\"https://www.openstreetmap.org/node/1\"", html);
        Assert.Contains("© OpenStreetMap-bijdragers (ODbL 1.0)", html);
        Assert.Contains("laatst geverifieerd 2026-07-01", html);
        Assert.Contains("abc123", html);
        Assert.Contains("href=\"/trace/abc123\"", html);
        Assert.Contains("Wilt u meer weten?", html);
        Assert.Contains("policy 1.0.1", html);
    }

    [Theory]
    [InlineData(TraceOutcome.RefusedMedical, "huisarts")]
    [InlineData(TraceOutcome.RefusedScope, "buiten de sociale kaart")]
    [InlineData(TraceOutcome.Escalated, "geen betrouwbare bron")]
    public void Renders_refusal_and_escalation_messages(TraceOutcome outcome, string expected)
    {
        var msg = outcome switch { TraceOutcome.RefusedMedical => Core.Policy.RefusalTexts.Medical, TraceOutcome.RefusedScope => Core.Policy.RefusalTexts.OutOfScope, _ => Core.Policy.RefusalTexts.Escalated };
        var html = AskHtml.Render(new AskResult("id0", outcome, msg, null, [], "1.0.1"));
        Assert.Contains(expected, html);
        Assert.Contains("outcome-" + outcome.ToString().ToLowerInvariant(), html);
        Assert.DoesNotContain("badge-feit", html);
    }

    [Fact]
    public void Unsafe_source_url_is_not_linked()
    {
        var r = new AskResult("id0", TraceOutcome.Answered, null, new Answer([new("t", "fact", ["c1"])], "high", null),
            [new SourceRef("c1", "s", "javascript:alert(1)", "H", null, null)], "1.0.1");
        var html = AskHtml.Render(r);
        Assert.DoesNotContain("href=\"javascript:", html);
    }
}
