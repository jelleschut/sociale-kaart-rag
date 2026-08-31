using System.Text.RegularExpressions;

namespace SocialeKaartRag.Core.SocialMap;

/// <summary>Vaste taxonomie (spec §4.1) en de mapping van bronvelden naar categorieën. Onbekend → leeg (niet indexeren).
/// Sinds ADR-0006 levert een fragment een geordende <em>set</em> categorieën: de eerste is de primaire (attributie,
/// logging), de rest verbreedt de boost in de zoekindex. De enkelvoudige methoden blijven bestaan en geven de
/// primaire categorie terug.</summary>
public static class Taxonomy
{
    public static readonly string[] Categories = ["gezondheid", "werk_inkomen", "wonen", "vervoer", "welzijn", "mantelzorg"];

    /// <summary>office=* wordt bewust nooit gemapt: in Den Haag zijn dat vooral internationale organisaties/tribunalen
    /// (ambassades, ngo's als internationale hoven), geen sociale kaart-doelgroep. De parameter blijft in de
    /// signatuur zodat een aanroeper 'm expliciet meegeeft en zichtbaar is dat het bewust genegeerd wordt.</summary>
    public static string? FromOsm(string? amenity, string? healthcare, string? socialFacility, string? office)
        => AllFromOsm(amenity, healthcare, socialFacility, office).FirstOrDefault();

    /// <summary>Alle categorieën voor een OSM-object, primaire eerst. Een woonvorm mét zorg (verpleeghuis, begeleid
    /// wonen) is zowel `wonen` als `gezondheid`; opvang is zowel `wonen` als `welzijn`; dagopvang en hulpverlening
    /// liggen op de grens van zorg en welzijn. De primaire categorie is per bedoeling ongewijzigd t.o.v. v1.</summary>
    public static string[] AllFromOsm(string? amenity, string? healthcare, string? socialFacility, string? office)
    {
        if (socialFacility is "assisted_living" or "group_home" or "nursing_home") return ["wonen", "gezondheid"];
        if (socialFacility is "shelter") return ["wonen", "welzijn"];
        if (socialFacility is "day_care") return ["gezondheid", "welzijn"];
        if (socialFacility is not null || amenity is "social_facility" or "community_centre" or "social_centre") return ["welzijn"];
        if (healthcare is "counselling") return ["welzijn", "gezondheid"];
        if (healthcare is "dentist") return [];
        if (healthcare is not null || amenity is "doctors" or "pharmacy" or "clinic" or "hospital") return ["gezondheid"];
        // office=* bewust niet gemapt: in Den Haag zijn dat vooral internationale organisaties/tribunalen,
        // geen sociale kaart-doelgroep (parameter blijft voor documentatie/signatuurstabiliteit).
        return [];
    }

    // Specifiek vóór generiek. Trefwoorden op titel+samenvatting; het SC-onderwerp is te grof en vaak leeg.
    // Volgorde: mantelzorg, vervoer, wonen, werk_inkomen, welzijn, gezondheid — werk_inkomen vóór welzijn zodat
    // "meedoenregeling"/"toeslag" niet op het welzijn-trefwoord "meedoen" blijft hangen; wonen vóór werk_inkomen
    // zodat "huurtoeslag" wonen blijft. Die volgorde bepaalt nu ook welke categorie de primaire is.
    private static readonly (string Category, Regex Pattern)[] ScRules =
    [
        ("mantelzorg", Rx(@"mantelzorg|respijt")),
        ("vervoer", Rx(@"gehandicaptenparkeer|regiotaxi|wmo-vervoer|vervoersvoorziening|scootmobiel|rolstoel|\bvervoer\b|leerlingenvervoer")),
        ("wonen", Rx(@"urgentie|woning|huisvesting|huurtoeslag|woonkosten|dakloos|\bopvang\b|beschermd wonen|woningaanpassing|\bwonen\b|woonwagen|woon-")),
        ("werk_inkomen", Rx(@"bijstand|schuld|inkomen|uitkering|kredietbank|armoede|minima|kwijtschelding|werkzoek|werkloos|werk en inkomen|re-integratie|\bbaan\b|voordeelpas|geld lenen|meedoenregeling|toeslag|tegemoetkoming")),
        ("welzijn", Rx(@"vrijwillig|welzijn|buurthuis|wijkcentrum|eenzaam|ontmoet|meedoen|inburger|taalles|maatschappelijk")),
        ("gezondheid", Rx(@"\bwmo\b|zorg\b|zorgverzekering|zorgtoeslag|zorgverlening|gezondheidszorg|gezondheid|hulpmiddel|thuiszorg|ggd|verslaving|jeugdhulp|dagbesteding|huishoudelijke hulp|hulp bij het huishouden")),
    ];

    private static readonly Dictionary<string, string> ScSubjects = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Sociale zekerheid"] = "werk_inkomen", ["Zorg en gezondheid"] = "gezondheid", ["Huisvesting"] = "wonen", ["Migratie en integratie"] = "welzijn",
    };

    public static string? FromSamenwerkendeCatalogi(string? subject, string title, string abstractText)
        => AllFromSamenwerkendeCatalogi(subject, title, abstractText).FirstOrDefault();

    /// <summary>Alle categorieën voor een SC-product, primaire eerst: élke matchende trefwoordregel (in de vaste
    /// volgorde hierboven), gevolgd door de SC-onderwerpmapping als die nog niet in de set zit. Het onderwerp is
    /// daarmee geen fallback-alleen meer, maar een extra categorie — zonder trefwoordmatch is het nog steeds de
    /// enige en dus de primaire. Voorbeeld: "ZoetermeerPas — voordeelpas … meedoen" → [werk_inkomen, welzijn].</summary>
    public static string[] AllFromSamenwerkendeCatalogi(string? subject, string title, string abstractText)
    {
        var text = (title + " " + abstractText).ToLowerInvariant();
        var found = new List<string>();
        foreach (var (category, pattern) in ScRules)
            if (pattern.IsMatch(text)) found.Add(category);
        if (subject is not null && ScSubjects.TryGetValue(subject, out var c) && !found.Contains(c)) found.Add(c);
        return [.. found];
    }

    private static Regex Rx(string p) => new(p, RegexOptions.IgnoreCase | RegexOptions.Compiled);
}
