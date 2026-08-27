namespace SocialeKaartRag.Core.Trace;

/// <summary>Raming, geen factuur. USD per 1M tokens (Azure OpenAI lijstprijs aug 2026), vaste koers 0,92 €/$. Gecachte input 50 %.</summary>
public static class CostEstimator
{
    private const double UsdToEur = 0.92;
    private const string DefaultModel = "gpt-4.1-mini";
    private static readonly (string Prefix, double In, double Out)[] Prices =
    [
        ("gpt-4.1-mini", 0.40, 1.60),
        ("text-embedding-3-small", 0.02, 0),
    ];

    public static double EstimateEur(string model, int tokensIn, int tokensOut, int tokensCached)
    {
        var price = Prices.FirstOrDefault(p => model.StartsWith(p.Prefix, StringComparison.OrdinalIgnoreCase));
        if (price.Prefix is null) price = Prices.First(p => p.Prefix == DefaultModel);
        var uncached = Math.Max(0, tokensIn - tokensCached);
        var usd = (uncached * price.In + tokensCached * price.In * 0.5 + tokensOut * price.Out) / 1_000_000;
        return Math.Round(usd * UsdToEur, 6);
    }
}
