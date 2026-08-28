using SocialeKaartRag.Eval;

namespace SocialeKaartRag.Eval.Tests;

public class EvalRunnerTests
{
    private static EvalCase Case(string id, string category) => new() { Id = id, Category = category, Question = "vraag " + id };

    [Fact]
    public void SplitCases_haalt_injection_cases_apart_en_behoudt_volgorde_per_groep()
    {
        var g08 = Case("g08", "groundedness");
        var inj01 = Case("inj01", "injection");
        var r01 = Case("r01", "refusal");
        var cases = new List<EvalCase> { g08, inj01, r01 };

        var (withoutCanaries, injection) = EvalRunner.SplitCases(cases);

        Assert.Equal(["g08", "r01"], withoutCanaries.Select(c => c.Id));
        Assert.Equal(["inj01"], injection.Select(c => c.Id));
        Assert.All(withoutCanaries, c => Assert.NotEqual("injection", c.Category));
        Assert.All(injection, c => Assert.Equal("injection", c.Category));
    }

    [Fact]
    public void ReorderResults_zet_resultaten_terug_in_de_oorspronkelijke_case_volgorde()
    {
        var g08 = Case("g08", "groundedness");
        var inj01 = Case("inj01", "injection");
        var r01 = Case("r01", "refusal");
        var cases = new List<EvalCase> { g08, inj01, r01 };

        // Simuleert de run-volgorde: eerst niet-injectie (g08, r01), dan injectie (inj01).
        var results = new List<CaseResult>
        {
            new("g08", "groundedness", true, "ok", 0.01),
            new("r01", "refusal", true, "ok", 0.0),
            new("inj01", "injection", true, "geen lek", 0.0),
        };

        var reordered = EvalRunner.ReorderResults(cases, results);

        Assert.Equal(["g08", "inj01", "r01"], reordered.Select(r => r.Id));
    }
}
