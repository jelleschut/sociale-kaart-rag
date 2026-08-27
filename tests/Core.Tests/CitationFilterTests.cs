using SocialeKaartRag.Core.Generation;
using SocialeKaartRag.Core.Policy;

namespace SocialeKaartRag.Core.Tests;

public class CitationFilterTests
{
    private static readonly HashSet<string> Known = ["c1", "c2"];

    [Fact]
    public void Removes_items_without_citation_and_unknown_citations()
    {
        var answer = new Answer(
        [
            new("met bron", "fact", ["c1"]),
            new("zonder bron", "summary", []),
            new("onbekende bron", "fact", ["c9"]),
            new("gemengd", "summary", ["c9", "c2"]),
        ], "high", null);

        var filtered = CitationFilter.Apply(answer, Known);

        Assert.Equal(["met bron", "gemengd"], filtered.Items.Select(i => i.Text));
        Assert.Equal(["c2"], filtered.Items[1].Citations);
        Assert.Equal("high", filtered.Confidence);
    }

    [Fact]
    public void Empty_after_filter_means_nothing_to_answer()
    {
        var answer = new Answer([new("x", "fact", ["zzz"])], "high", null);
        Assert.Empty(CitationFilter.Apply(answer, Known).Items);
    }
}
