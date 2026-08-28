using System.Text.RegularExpressions;

namespace SocialeKaartRag.Core.SocialMap;

/// <summary>Vaste taxonomie (spec §4.1) en de mapping van bronvelden naar categorie. Onbekend → null (niet indexeren).</summary>
public static class Taxonomy
{
    public static readonly string[] Categories = ["gezondheid", "werk_inkomen", "wonen", "vervoer", "welzijn", "mantelzorg"];

    public static string? FromOsm(string? amenity, string? healthcare, string? socialFacility, string? office)
    {
        if (socialFacility is "assisted_living" or "group_home" or "nursing_home" or "shelter") return "wonen";
        if (socialFacility is "day_care") return "mantelzorg";
        if (socialFacility is not null || amenity is "social_facility" or "community_centre" or "social_centre") return "welzijn";
        if (healthcare is "counselling") return "welzijn";
        if (healthcare is not null || amenity is "doctors" or "pharmacy" or "clinic" or "hospital" or "dentist") return "gezondheid";
        if (office is "charity" or "ngo" or "association") return "welzijn";
        return null;
    }

    // Specifiek vóór generiek. Trefwoorden op titel+samenvatting; het SC-onderwerp is te grof en vaak leeg.
    private static readonly (string Category, Regex Pattern)[] ScRules =
    [
        ("mantelzorg", Rx(@"mantelzorg|respijt")),
        ("vervoer", Rx(@"gehandicaptenparkeer|regiotaxi|wmo-vervoer|vervoersvoorziening|scootmobiel|rolstoel")),
        ("wonen", Rx(@"urgentie|woning|huisvesting|huurtoeslag|woonkosten|dakloos|\bopvang\b|beschermd wonen|woningaanpassing")),
        ("welzijn", Rx(@"vrijwillig|welzijn|buurthuis|wijkcentrum|eenzaam|ontmoet|meedoen|inburger|taalles|maatschappelijk")),
        ("werk_inkomen", Rx(@"bijstand|schuld|inkomen|uitkering|kredietbank|armoede|minima|kwijtschelding|\bwerk\b|werkzoek|re-integratie|voordeelpas|geld lenen")),
        ("gezondheid", Rx(@"\bwmo\b|zorg|gezondheid|hulpmiddel|thuiszorg|ggd|verslaving|jeugdhulp|dagbesteding|huishoudelijke hulp|hulp bij het huishouden")),
    ];

    private static readonly Dictionary<string, string> ScSubjects = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Sociale zekerheid"] = "werk_inkomen", ["Zorg en gezondheid"] = "gezondheid", ["Huisvesting"] = "wonen", ["Migratie en integratie"] = "welzijn",
    };

    public static string? FromSamenwerkendeCatalogi(string? subject, string title, string abstractText)
    {
        var text = (title + " " + abstractText).ToLowerInvariant();
        foreach (var (category, pattern) in ScRules)
            if (pattern.IsMatch(text)) return category;
        return subject is not null && ScSubjects.TryGetValue(subject, out var c) ? c : null;
    }

    private static Regex Rx(string p) => new(p, RegexOptions.IgnoreCase | RegexOptions.Compiled);
}
