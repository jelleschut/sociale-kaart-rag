using System.Text.RegularExpressions;

namespace SocialeKaartRag.Core.Policy;

public sealed record PiiResult(string Text, bool Redacted, IReadOnlyList<string> Types);

/// <summary>Regex-PII-filter op de vraag (spec §4.3 stap 1). Alleen typen worden gelogd, nooit waarden.</summary>
public static partial class PiiFilter
{
    [GeneratedRegex(@"(?<!\d)\d{9}(?!\d)")]
    private static partial Regex Bsn();

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial Regex Email();

    // NL mobiel (06 / +31 6) en vast (0xx-xxxxxxx / 0xxx-xxxxxx), met optionele spaties/streepjes
    [GeneratedRegex(@"(?<!\d)(?:\+31|0031|0)[\s-]?6[\s-]?\d{8}(?!\d)|(?<!\d)(?:\+31|0031|0)[\s-]?\d{2,3}[\s-]?\d{6,7}(?!\d)")]
    private static partial Regex Phone();

    // postcode gevolgd door huisnummer (evt. toevoeging) = adres; postcode alleen blijft staan (grofmazige locatie mag)
    [GeneratedRegex(@"(?<!\d)\d{4}\s?[A-Za-z]{2}\s+\d{1,5}[A-Za-z]?(?![A-Za-z0-9])")]
    private static partial Regex Address();

    public static PiiResult Redact(string input)
    {
        var types = new List<string>();
        var text = input;

        text = Bsn().Replace(text, m => IsValidBsn(m.Value) ? Mark(types, "bsn") : m.Value);
        text = Email().Replace(text, _ => Mark(types, "email"));
        text = Address().Replace(text, _ => Mark(types, "address"));
        text = Phone().Replace(text, _ => Mark(types, "phone"));

        return new PiiResult(text, types.Count > 0, types.Distinct().ToList());
    }

    /// <summary>11-proef: som(cijfer_i × gewicht_i) met gewichten 9..2 en −1 voor het laatste cijfer, deelbaar door 11.</summary>
    public static bool IsValidBsn(string digits)
    {
        if (digits.Length != 9 || !digits.All(char.IsAsciiDigit)) return false;
        var sum = 0;
        for (var i = 0; i < 8; i++) sum += (digits[i] - '0') * (9 - i);
        sum -= digits[8] - '0';
        return sum % 11 == 0;
    }

    private static string Mark(List<string> types, string type) { types.Add(type); return $"[{type}]"; }
}
