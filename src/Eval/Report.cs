using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SocialeKaartRag.Eval;

public static class Report
{
    public static string Render(Summary s, IReadOnlyList<CaseResult> results, DateTimeOffset ranAt, string policyVersion, TimeSpan duration)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Eval-rapport sociale-kaart-rag");
        sb.AppendLine();
        sb.AppendLine($"Gedraaid: {ranAt:yyyy-MM-dd HH:mm} UTC · policyVersion {policyVersion} · duur {duration.TotalMinutes:F1} min · kosten van deze run: € {FormatNl(s.TotalCostEur, "0.000")}");
        sb.AppendLine();
        sb.AppendLine("| Categorie | Geslaagd | Score | Drempel | Status |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var cat in Cases.Categories)
        {
            if (s.Count(cat) == 0) continue;
            var th = Summary.Thresholds[cat];
            sb.AppendLine($"| {cat} | {s.PassedCount(cat)}/{s.Count(cat)} | {s.PassRate(cat) * 100:0} % | {th * 100:0} % | {(s.PassRate(cat) >= th ? "✅" : "❌")} |");
        }
        sb.AppendLine();
        sb.AppendLine(s.MeetsThresholds(out var failures) ? "**Alle drempels gehaald.**" : "**Drempels niet gehaald:** " + string.Join("; ", failures));
        sb.AppendLine();
        sb.AppendLine("## Per case");
        sb.AppendLine();
        sb.AppendLine("| Id | Categorie | Status | Toelichting | € |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var r in results) sb.AppendLine($"| {r.Id} | {r.Category} | {(r.Passed ? "✅" : "❌")} | {Truncate(Regex.Replace(r.Detail, @"\s+", " ").Replace("|", "/"), 240)} | {FormatNl(r.CostEur, "0.0000")} |");
        sb.AppendLine();
        sb.AppendLine("Vragen en antwoorden staan niet in dit rapport (data-minimalisatie, spec §4.5); de cases staan in `eval/cases.yaml`.");
        return sb.ToString();
    }

    // nl-NL gebruikt een komma als decimaalteken; onder invariant globalization (bv. CI op Linux)
    // valt CultureInfo.GetCultureInfo("nl-NL") terug op invariant, dus formatteer expliciet.
    private static string FormatNl(double value, string format) =>
        value.ToString(format, CultureInfo.InvariantCulture).Replace('.', ',');

    // lange judge-reasons of foutmeldingen mogen de tabelrij niet opblazen; toon de eerste 240 tekens.
    private static string Truncate(string s, int maxLength) =>
        s.Length <= maxLength ? s : s[..maxLength] + "…";
}
