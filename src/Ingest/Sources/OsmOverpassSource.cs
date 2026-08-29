using System.Text.Json;
using System.Text.RegularExpressions;
using SocialeKaartRag.Core.SocialMap;

namespace SocialeKaartRag.Ingest.Sources;

/// <summary>OpenStreetMap via Overpass (ODbL). Coördinaten afgerond op 3 decimalen (≈100 m), huisnummers niet overgenomen (spec §4.1).
/// office=* bewust weggelaten: in Den Haag zijn dat vooral internationale organisaties, geen sociale kaart.</summary>
public sealed partial class OsmOverpassSource(HttpClient http)
{
    public const string Endpoint = "https://overpass-api.de/api/interpreter";
    public const string Attribution = "© OpenStreetMap-bijdragers (ODbL 1.0)";

    /// <summary>OSM-naam van het gebied → gemeentenaam zoals opgeslagen in SocialMapRecord.Municipality.</summary>
    public static readonly (string OsmName, string Municipality)[] Municipalities =
    [
        ("Den Haag", "Den Haag"),
        ("Zoetermeer", "Zoetermeer"),
    ];

    /// <summary>Overpass QL geldig voor precies één gemeente, scoped op OSM-gebiedsnaam.</summary>
    public static string QueryFor(string municipalityOsmName) =>
        $"""
        [out:json][timeout:120];
        area["name"="{municipalityOsmName}"]["admin_level"="8"]->.a;
        (
          nwr(area.a)["amenity"~"^(social_facility|community_centre|social_centre)$"];
          nwr(area.a)["healthcare"];
        );
        out tags center;
        """;

    public async Task<(List<SocialMapRecord> Records, int Skipped)> FetchAllAsync(CancellationToken ct = default)
    {
        var records = new List<SocialMapRecord>();
        var skipped = 0;
        foreach (var (osmName, municipality) in Municipalities)
        {
            using var content = new FormUrlEncodedContent([new KeyValuePair<string, string>("data", QueryFor(osmName))]);
            using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = content };
            req.Headers.UserAgent.ParseAdd(PageFetcher.UserAgent);
            using var resp = await http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
            var (r, s) = ParseWithReport(await resp.Content.ReadAsStringAsync(ct), municipality);
            records.AddRange(r);
            skipped += s;
        }
        return (records, skipped);
    }

    public static List<SocialMapRecord> Parse(string json, string municipality) => ParseWithReport(json, municipality).Records;

    public static (List<SocialMapRecord> Records, int Skipped) ParseWithReport(string json, string municipality)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<SocialMapRecord>(); var skipped = 0;
        foreach (var e in doc.RootElement.GetProperty("elements").EnumerateArray())
        {
            if (!e.TryGetProperty("tags", out var tags)) { skipped++; continue; }
            string? T(string k) => tags.TryGetProperty(k, out var v) ? v.GetString() : null;
            var name = T("name");
            var categories = Taxonomy.AllFromOsm(T("amenity"), T("healthcare"), T("social_facility"), T("office"));
            if (string.IsNullOrWhiteSpace(name) || categories.Length == 0) { skipped++; continue; }
            var (lat, lon) = Coords(e);
            var type = e.GetProperty("type").GetString(); var id = e.GetProperty("id").GetInt64();
            list.Add(new SocialMapRecord
            {
                Source = "osm", SourceId = $"osm:{type}/{id}", SourceUrl = $"https://www.openstreetmap.org/{type}/{id}",
                Name = name, Category = categories[0], Categories = categories, Summary = Describe(tags),
                BodyText = T("description") is { } d ? PageFetcher.RemovePersonalContacts(d) : null,
                Municipality = municipality, Street = StreetWithoutNumber(T("addr:street")),
                Postcode = NormalisePostcode(T("addr:postcode")),
                Phone = RedactPhone(T("phone") ?? T("contact:phone")), Website = T("website") ?? T("contact:website"),
                OpeningHours = T("opening_hours"),
                Lat = lat is null ? null : Math.Round(lat.Value, 3), Lon = lon is null ? null : Math.Round(lon.Value, 3),
                Attribution = Attribution,
            });
        }
        return (list, skipped);
    }

    private static (double? Lat, double? Lon) Coords(JsonElement e)
    {
        if (e.TryGetProperty("lat", out var la) && e.TryGetProperty("lon", out var lo)) return (la.GetDouble(), lo.GetDouble());
        if (e.TryGetProperty("center", out var c)) return (c.GetProperty("lat").GetDouble(), c.GetProperty("lon").GetDouble());
        return (null, null);
    }

    // Mobiele nummers zijn persoonsgegevens en worden verwijderd (zelfde filter als paginatekst); vaste
    // organisatienummers blijven staan (ADR-0002).
    private static string? RedactPhone(string? phone)
    {
        if (phone is null) return null;
        var redacted = PageFetcher.RemovePersonalContacts(phone);
        return redacted == "[telefoon verwijderd]" ? null : redacted;
    }

    private static string? StreetWithoutNumber(string? street) => street is null ? null : TrailingNumber().Replace(street, "").Trim();
    private static string? NormalisePostcode(string? pc)
    {
        if (pc is null) return null;
        var s = pc.Replace(" ", "").ToUpperInvariant();
        return Postcode().IsMatch(s) ? s : null;
    }

    private static readonly Dictionary<string, string> TypeNames = new()
    {
        ["doctors"] = "huisartsenpraktijk", ["doctor"] = "huisartsenpraktijk", ["pharmacy"] = "apotheek", ["clinic"] = "kliniek", ["hospital"] = "ziekenhuis",
        ["physiotherapist"] = "fysiotherapie", ["community_centre"] = "wijkcentrum", ["social_facility"] = "sociale voorziening", ["social_centre"] = "ontmoetingscentrum",
        ["counselling"] = "hulpverlening/begeleiding", ["assisted_living"] = "begeleid wonen", ["nursing_home"] = "verpleeghuis", ["group_home"] = "woongroep",
        ["day_care"] = "dagopvang", ["outreach"] = "outreach/veldwerk", ["shelter"] = "opvang", ["alternative"] = "alternatieve zorg", ["psychotherapist"] = "psychotherapie",
        ["speech_therapist"] = "logopedie", ["podiatrist"] = "podotherapie", ["centre"] = "gezondheidscentrum",
    };

    /// <summary>Korte Nederlandse typering uit de tags, bv. "apotheek" of "sociale voorziening voor senior, disabled".</summary>
    private static string Describe(JsonElement tags)
    {
        string? T(string k) => tags.TryGetProperty(k, out var v) ? v.GetString() : null;
        var main = T("social_facility") ?? (T("amenity") is "doctors" or "pharmacy" or "clinic" or "hospital" or "community_centre" or "social_facility" or "social_centre" ? T("amenity") : null) ?? T("healthcare") ?? "";
        var label = TypeNames.GetValueOrDefault(main, main.Replace('_', ' '));
        var target = T("social_facility") is null ? null : T("social_facility:for");
        return target is null ? label : $"{label} voor {target.Replace('_', ' ').Replace(';', ',').Replace(",", ", ")}";
    }

    // Strips een trailend huisnummer van straatnamen. Bekende beperking (zeldzaam, geaccepteerd): pleinnamen
    // die zelf op een getal eindigen, bv. "Plein 1813", worden ook afgekapt tot "Plein".
    [GeneratedRegex(@"\s+\d+[A-Za-z\-]*$")] private static partial Regex TrailingNumber();
    [GeneratedRegex(@"^[1-9]\d{3}[A-Z]{2}$")] private static partial Regex Postcode();
}
