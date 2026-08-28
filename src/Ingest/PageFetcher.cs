using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace SocialeKaartRag.Ingest;

public sealed record FetchedPage(string Text, string? Keywords);

/// <summary>Haalt de gemeentepagina achter een SC-product op. Per-host-toestemming (ADR-0002): alleen zoetermeer.nl.
/// Max 1 request per 600 ms, eigen User-Agent, geen pdf. Persoonlijke contactgegevens worden weggefilterd.</summary>
public sealed partial class PageFetcher(HttpClient http)
{
    public const int MaxChars = 8000;
    public const string UserAgent = "sociale-kaart-rag/1.0 (+https://github.com/jelleschut/sociale-kaart-rag)";
    private static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(600);
    // denhaag.nl bewust afwezig: gebruiksvoorwaarden geven geen hergebruiksrecht (ADR-0002).
    private static readonly HashSet<string> AllowedHosts = ["www.zoetermeer.nl"];
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _last = DateTimeOffset.MinValue;

    public static bool IsAllowed(Uri u) => u.Scheme == "https" && AllowedHosts.Contains(u.Host) && !u.AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    public async Task<FetchedPage?> FetchAsync(Uri url, CancellationToken ct = default)
    {
        if (!IsAllowed(url)) return null;
        await _gate.WaitAsync(ct);
        try
        {
            var wait = _last + MinInterval - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd(UserAgent);
            using var resp = await http.SendAsync(req, ct);
            _last = DateTimeOffset.UtcNow;
            if (!resp.IsSuccessStatusCode) return null;
            return ExtractZoetermeer(await resp.Content.ReadAsStringAsync(ct));
        }
        finally { _gate.Release(); }
    }

    /// <summary>Next.js-pagina: tekst uit __NEXT_DATA__ (title, fieldIntroduction, alle fieldParagraphs…processed, keywords-metatag).</summary>
    public static FetchedPage? ExtractZoetermeer(string html)
    {
        var m = NextData().Match(html);
        if (!m.Success) return null;
        using var doc = JsonDocument.Parse(m.Groups[1].Value);
        if (!TryPath(doc.RootElement, out var node, "props", "pageProps", "contentDetails", "data", "route", "nodeContext")) return null;

        var parts = new List<string>();
        if (node.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String) parts.Add(t.GetString()! + ".");
        if (node.TryGetProperty("fieldIntroduction", out var intro) && intro.ValueKind != JsonValueKind.Null)
            parts.Add(HtmlToText(intro.ValueKind == JsonValueKind.String ? intro.GetString()! : intro.TryGetProperty("processed", out var ip) ? ip.GetString() ?? "" : ""));
        if (node.TryGetProperty("fieldParagraphs", out var paras)) CollectProcessed(paras, parts);

        string? keywords = null;
        if (node.TryGetProperty("entityMetatags", out var metas) && metas.ValueKind == JsonValueKind.Array)
            foreach (var meta in metas.EnumerateArray())
            {
                var isKeywords = (meta.TryGetProperty("name", out var n) && n.GetString() == "keywords")
                    || (meta.TryGetProperty("key", out var k) && k.GetString() == "keywords");
                if (isKeywords && meta.TryGetProperty("value", out var v)) keywords = v.GetString();
            }

        var text = Whitespace().Replace(string.Join(" ", parts.Where(p => p.Length > 0)), " ").Trim();
        text = RemovePersonalContacts(text);
        return new FetchedPage(text.Length > MaxChars ? text[..MaxChars] : text, keywords);
    }

    // Verzamelt in documentvolgorde: fieldTitle (kopjes) en elke "processed"-HTML onder fieldParagraphs, ongeacht de nesting.
    private static void CollectProcessed(JsonElement el, List<string> parts)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Array: foreach (var x in el.EnumerateArray()) CollectProcessed(x, parts); break;
            case JsonValueKind.Object:
                foreach (var p in el.EnumerateObject())
                {
                    if (p.Name == "fieldTitle" && p.Value.ValueKind == JsonValueKind.String) parts.Add(p.Value.GetString()!);
                    else if (p.Name == "processed" && p.Value.ValueKind == JsonValueKind.String) parts.Add(HtmlToText(p.Value.GetString()!));
                    else CollectProcessed(p.Value, parts);
                }
                break;
        }
    }

    private static bool TryPath(JsonElement root, out JsonElement result, params string[] path)
    {
        result = root;
        foreach (var p in path) { if (result.ValueKind != JsonValueKind.Object || !result.TryGetProperty(p, out result)) return false; }
        return true;
    }

    private static string HtmlToText(string html)
    {
        var doc = new HtmlDocument(); doc.LoadHtml(html);
        return Whitespace().Replace(HtmlEntity.DeEntitize(doc.DocumentNode.InnerText), " ").Trim();
    }

    /// <summary>Organisatie-contact blijft (algemene nummers, info@/…); mobiele nummers en persoonlijke e-mails (voornaam.achternaam@) gaan weg.</summary>
    public static string RemovePersonalContacts(string text)
    {
        text = MobilePhone().Replace(text, "[telefoon verwijderd]");
        text = PersonalEmail().Replace(text, "[e-mail verwijderd]");
        return text;
    }

    [GeneratedRegex(@"<script id=""__NEXT_DATA__"" type=""application/json""[^>]*>(.*?)</script>", RegexOptions.Singleline)] private static partial Regex NextData();
    [GeneratedRegex(@"\s+")] private static partial Regex Whitespace();
    [GeneratedRegex(@"(?<!\d)(?:\+31|0031|0)[\s-]?6(?:[\s-]?\d){8}(?!\d)")] private static partial Regex MobilePhone();
    [GeneratedRegex(@"\b[a-z]+\.[a-z]+(?:\.[a-z]+)?@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.IgnoreCase)] private static partial Regex PersonalEmail();
}
