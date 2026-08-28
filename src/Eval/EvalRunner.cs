namespace SocialeKaartRag.Eval;

/// <summary>Pure ordeningslogica voor de eval-run, los van I/O zodat hij testbaar is (zie spec-issue: canaries stonden
/// de hele run in de index, waardoor een groundedness-case per ongeluk canary-charlie citeerde en de judge-prompt op
/// Azure's content filter liep). Canaries horen alleen aanwezig te zijn tijdens de injection-cases.</summary>
public static class EvalRunner
{
    /// <summary>Splitst cases in niet-injectie (draaien zonder canaries in de index) en injectie-cases (draaien
    /// terwijl canaries geseed zijn). Behoudt de oorspronkelijke onderlinge volgorde binnen elke groep.</summary>
    public static (IReadOnlyList<EvalCase> WithoutCanaries, IReadOnlyList<EvalCase> Injection) SplitCases(IReadOnlyList<EvalCase> cases)
    {
        var withoutCanaries = cases.Where(c => c.Category != "injection").ToList();
        var injection = cases.Where(c => c.Category == "injection").ToList();
        return (withoutCanaries, injection);
    }

    /// <summary>Herordent resultaten terug naar de oorspronkelijke case-volgorde, zodat het rapport leesbaar blijft
    /// ongeacht in welke volgorde de cases daadwerkelijk zijn uitgevoerd (niet-injectie eerst, dan injectie).</summary>
    public static IReadOnlyList<CaseResult> ReorderResults(IReadOnlyList<EvalCase> cases, IReadOnlyList<CaseResult> results)
    {
        var order = cases.Select((c, i) => (c.Id, i)).ToDictionary(x => x.Id, x => x.i);
        return results.OrderBy(r => order.TryGetValue(r.Id, out var i) ? i : int.MaxValue).ToList();
    }
}
