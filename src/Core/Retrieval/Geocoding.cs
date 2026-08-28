using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SocialeKaartRag.Core.Retrieval;

public sealed record GeoPoint(double Lat, double Lon);

public interface IGeocoder
{
    Task<GeoPoint?> PostcodeAsync(string postcode, CancellationToken ct = default);
    Task<GeoPoint?> MunicipalityCentroidAsync(string municipality, CancellationToken ct = default);
}

/// <summary>PDOK Locatieserver v3_1 (CC0, geen key). Alleen postcode-/gemeenteniveau (spec: grofmazige locatie).</summary>
public sealed partial class PdokGeocoder(HttpClient http) : IGeocoder
{
    private const string Base = "https://api.pdok.nl/bzk/locatieserver/search/v3_1/free";
    public Task<GeoPoint?> PostcodeAsync(string postcode, CancellationToken ct = default) =>
        Lookup($"{Base}?q={Uri.EscapeDataString(postcode)}&fq=type:postcode&rows=1&fl=centroide_ll", ct);
    public Task<GeoPoint?> MunicipalityCentroidAsync(string municipality, CancellationToken ct = default) =>
        Lookup($"{Base}?q={Uri.EscapeDataString(municipality)}&fq=type:gemeente&rows=1&fl=centroide_ll", ct);
    private async Task<GeoPoint?> Lookup(string url, CancellationToken ct) => ParsePoint(await http.GetStringAsync(url, ct));

    public static GeoPoint? ParsePoint(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var docs = doc.RootElement.GetProperty("response").GetProperty("docs");
        if (docs.GetArrayLength() == 0) return null;
        var m = PointRx().Match(docs[0].GetProperty("centroide_ll").GetString() ?? "");
        return m.Success
            ? new GeoPoint(double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
            : null;
    }

    [GeneratedRegex(@"POINT\(([-\d.]+) ([-\d.]+)\)")] private static partial Regex PointRx();
}

/// <summary>Postcode in een vraag (bv. "in de buurt van 2511CV"); postcode zonder huisnummer is geen PII (PiiFilter laat hem staan).</summary>
public static partial class PostcodeDetector
{
    // Geldige postcodeletters sluiten F, I, O, Q, U, Y uit (PostNL-conventie, ter vermijding van verwarring met
    // cijfers/andere tekens) — dat voorkomt dat toevallige tweeletterwoorden na een jaartal ("2023 is") als postcode gelden.
    [GeneratedRegex(@"(?<!\d)([1-9]\d{3})\s?([ABCDEGHJKLMNPRSTVWXZabcdeghjklmnprstvwxz]{2})(?![A-Za-z0-9])")] private static partial Regex Rx();
    public static string? Find(string text) { var m = Rx().Match(text); return m.Success ? (m.Groups[1].Value + m.Groups[2].Value).ToUpperInvariant() : null; }
}
