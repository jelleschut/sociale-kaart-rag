using SocialeKaartRag.Eval;

namespace SocialeKaartRag.Eval.Tests;

public class CasesTests
{
    private static string CasesPath => Path.Combine(AppContext.BaseDirectory, "eval", "cases.yaml");

    [Fact]
    public void Loads_all_cases_with_unique_ids_and_valid_categories()
    {
        var cases = Cases.Load(CasesPath);
        Assert.True(cases.Count >= 30);
        Assert.Equal(cases.Count, cases.Select(c => c.Id).Distinct().Count());
        Assert.All(cases, c => Assert.Contains(c.Category, Cases.Categories));
        Assert.All(cases, c => Assert.False(string.IsNullOrWhiteSpace(c.Question)));
    }

    [Fact]
    public void Every_category_has_at_least_three_cases_and_every_guardrail_a_red_route()
    {
        var cases = Cases.Load(CasesPath);
        foreach (var cat in Cases.Categories) Assert.True(cases.Count(c => c.Category == cat) >= 3, cat);
        Assert.Contains(cases, c => c.Category == "refusal" && c.Expect.Outcome == "refused_medical");
        Assert.Contains(cases, c => c.Category == "refusal" && c.Expect.Outcome == "refused_scope");
        Assert.Contains(cases, c => c.Category == "injection" && c.Expect.ForbiddenOutput is not null);
        Assert.Contains(cases, c => c.Category == "pii" && c.Expect.PiiTypes.Contains("bsn"));
        Assert.Contains(cases, c => c.Category == "provenance" && c.Expect.Outcome == "escalated");
    }

    [Fact]
    public void Parses_expectations()
    {
        var yaml = """
        - id: t1
          category: groundedness
          question: waar is een wijkcentrum bij 2511CV?
          expect:
            outcome: answered
            source_url_contains: openstreetmap.org
            judge: true
        """;
        var c = Assert.Single(Cases.Parse(yaml));
        Assert.Equal("groundedness", c.Category);
        Assert.Equal("answered", c.Expect.Outcome);
        Assert.Equal("openstreetmap.org", c.Expect.SourceUrlContains);
        Assert.True(c.Expect.Judge);
    }
}
