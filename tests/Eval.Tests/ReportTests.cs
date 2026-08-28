using SocialeKaartRag.Eval;

namespace SocialeKaartRag.Eval.Tests;

public class ReportTests
{
    [Fact]
    public void Renders_table_thresholds_and_costs()
    {
        var results = new[] { new CaseResult("r1", "refusal", true, "", 0.001), new CaseResult("g1", "groundedness", false, "claim x niet in bron", 0.002) };
        var md = Report.Render(Scoring.Summarise(results), results, new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero), "1.0.0", TimeSpan.FromMinutes(3));
        Assert.Contains("| refusal | 1/1 | 100 % | 100 % | ✅ |", md);
        Assert.Contains("| groundedness | 0/1 | 0 % | 90 % | ❌ |", md);
        Assert.Contains("€ 0,003", md);
        Assert.Contains("g1", md);
        Assert.Contains("policyVersion 1.0.0", md);
    }

    [Fact]
    public void Whitespace_in_detail_is_collapsed_so_the_table_row_stays_on_one_line()
    {
        var results = new[] { new CaseResult("r1", "refusal", true, "regel een\r\nregel  twee\ten\tdrie", 0.001) };
        var md = Report.Render(Scoring.Summarise(results), results, new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero), "1.0.0", TimeSpan.FromMinutes(3));
        Assert.Contains("| regel een regel twee en drie |", md);
    }
}
