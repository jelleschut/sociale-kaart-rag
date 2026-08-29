using SocialeKaartRag.Core.SocialMap;

namespace SocialeKaartRag.Core.Tests;

public class TaxonomyTests
{
    [Theory]
    [InlineData("doctors", null, null, null, "gezondheid")]
    [InlineData(null, "pharmacy", null, null, "gezondheid")]
    [InlineData("social_facility", null, "assisted_living", null, "wonen")]
    [InlineData("social_facility", null, "nursing_home", null, "wonen")]
    [InlineData("social_facility", null, "shelter", null, "wonen")]
    [InlineData("social_facility", null, "outreach", null, "welzijn")]
    [InlineData("social_facility", null, null, null, "welzijn")]
    [InlineData("community_centre", null, null, null, "welzijn")]
    [InlineData(null, "counselling", null, null, "welzijn")]
    [InlineData("social_facility", null, "day_care", null, "gezondheid")]
    public void Maps_osm_tags(string? amenity, string? healthcare, string? facility, string? office, string expected)
        => Assert.Equal(expected, Taxonomy.FromOsm(amenity, healthcare, facility, office));

    [Theory]
    [InlineData("cafe", null, null, null)]
    [InlineData(null, null, null, "company")]
    [InlineData(null, null, null, null)]
    [InlineData("dentist", "dentist", null, null)]
    [InlineData(null, null, null, "ngo")]
    [InlineData(null, null, null, "charity")]
    [InlineData(null, null, null, "association")]
    public void Unknown_osm_tags_map_to_null(string? amenity, string? healthcare, string? facility, string? office)
        => Assert.Null(Taxonomy.FromOsm(amenity, healthcare, facility, office));

    [Theory]
    [InlineData("Sociale zekerheid", "Bijzondere bijstand aanvragen", "", "werk_inkomen")]
    [InlineData("Zorg en gezondheid", "Wmo-ondersteuning", "", "gezondheid")]
    [InlineData("Huisvesting", "Urgentieverklaring woning", "", "wonen")]
    [InlineData(null, "Parkeervergunning voor mantelzorgers aanvragen", "", "mantelzorg")]
    [InlineData(null, "Geld lenen bij de Gemeentelijke Kredietbank", "Om schulden af te betalen", "werk_inkomen")]
    [InlineData(null, "Gehandicaptenparkeerkaart", "", "vervoer")]
    [InlineData(null, "ZoetermeerPas", "voordeelpas voor inwoners met een laag inkomen", "werk_inkomen")]
    [InlineData(null, "Vrijwilligerswerk", "onbetaald werk voor de samenleving", "welzijn")]
    [InlineData(null, "Hulp bij het huishouden aanvragen", "Via de Wmo", "gezondheid")]
    [InlineData(null, "Regiotaxi aanvragen", "", "vervoer")]
    [InlineData("Recht", "Getuigen aanmelden", "Bij uw huwelijk heeft u getuigen nodig", null)]
    [InlineData("Bestuur", "Subsidie duurzame wijkactie aanvragen", "Voor een duurzaam project in uw wijk.", null)]
    [InlineData(null, "Melding doen over dieren", "Vindt u een zwerfkat?", null)]
    [InlineData("Natuur en milieu", "Subsidie voor opvangen regenwater", "regenton of groen dak", null)]
    [InlineData(null, "Meedoenregeling", "Bijdrage voor inwoners met een laag inkomen", "werk_inkomen")]
    [InlineData(null, "Leerlingenvervoer aanvragen", "", "vervoer")]
    [InlineData(null, "Woonwagenstandplaats", "", "wonen")]
    [InlineData(null, "Kinderopvangtoeslag", "Tegemoetkoming in de kosten", "werk_inkomen")]
    [InlineData(null, "Dagbesteding ouderen", "", "gezondheid")]
    [InlineData(null, "Zorgen voor uw tuin", "Groenonderhoud", null)]
    [InlineData(null, "Verzorgingsplaats langs de A4", "", null)]
    [InlineData(null, "Collectieve zorgverzekering minima", "", "werk_inkomen")]
    [InlineData(null, "Thuiszorg aanvragen", "", "gezondheid")]
    [InlineData(null, "Respijtzorg voor mantelzorgers", "", "mantelzorg")]
    public void Maps_sc_subject_and_keywords(string? subject, string title, string abstractText, string? expected)
        => Assert.Equal(expected, Taxonomy.FromSamenwerkendeCatalogi(subject, title, abstractText));

    [Theory]
    // De regressie die ADR-0006 uitlokte: de ZoetermeerPas is een inkomensregeling én een meedoen-voorziening.
    // Onder één categorie sneed het welzijn-filter hem weg; nu draagt hij beide, primaire eerst.
    [InlineData(null, "ZoetermeerPas", "Met de voordeelpas kunt u meedoen aan activiteiten", new[] { "werk_inkomen", "welzijn" })]
    [InlineData(null, "Meedoenregeling", "Bijdrage voor inwoners met een laag inkomen", new[] { "werk_inkomen", "welzijn" })]
    [InlineData(null, "Beschermd wonen aanvragen", "Wonen met begeleiding vanuit de Wmo", new[] { "wonen", "gezondheid" })]
    [InlineData(null, "Gehandicaptenparkeerkaart", "", new[] { "vervoer" })]
    // Onderwerp als extra categorie i.p.v. fallback-alleen: de trefwoorden geven welzijn, het SC-onderwerp wonen.
    [InlineData("Huisvesting", "Taalles voor nieuwkomers", "", new[] { "welzijn", "wonen" })]
    // Zonder trefwoordtreffer blijft het onderwerp de enige — en dus de primaire — categorie.
    [InlineData("Migratie en integratie", "Aanmelden bij het loket", "", new[] { "welzijn" })]
    [InlineData(null, "Melding doen over dieren", "Vindt u een zwerfkat?", new string[0])]
    public void Sc_keywords_yield_every_matching_category_primary_first(string? subject, string title, string abstractText, string[] expected)
        => Assert.Equal(expected, Taxonomy.AllFromSamenwerkendeCatalogi(subject, title, abstractText));

    [Fact]
    public void Sc_primary_category_is_the_first_of_the_set()
    {
        var all = Taxonomy.AllFromSamenwerkendeCatalogi(null, "ZoetermeerPas", "voordeelpas om mee te kunnen meedoen");
        Assert.Equal(all.FirstOrDefault(), Taxonomy.FromSamenwerkendeCatalogi(null, "ZoetermeerPas", "voordeelpas om mee te kunnen meedoen"));
    }

    [Theory]
    // Woonvormen mét zorg en opvang liggen op twee domeinen tegelijk; de primaire blijft ongewijzigd t.o.v. v1.
    [InlineData("social_facility", null, "nursing_home", new[] { "wonen", "gezondheid" })]
    [InlineData("social_facility", null, "shelter", new[] { "wonen", "welzijn" })]
    [InlineData("social_facility", null, "day_care", new[] { "gezondheid", "welzijn" })]
    [InlineData(null, "counselling", null, new[] { "welzijn", "gezondheid" })]
    [InlineData("community_centre", null, null, new[] { "welzijn" })]
    [InlineData(null, "pharmacy", null, new[] { "gezondheid" })]
    [InlineData("cafe", null, null, new string[0])]
    public void Osm_tags_yield_every_matching_category_primary_first(string? amenity, string? healthcare, string? facility, string[] expected)
    {
        Assert.Equal(expected, Taxonomy.AllFromOsm(amenity, healthcare, facility, null));
        Assert.Equal(expected.FirstOrDefault(), Taxonomy.FromOsm(amenity, healthcare, facility, null));
    }

    [Fact]
    public void Categories_are_the_fixed_six()
        => Assert.Equal(["gezondheid", "werk_inkomen", "wonen", "vervoer", "welzijn", "mantelzorg"], Taxonomy.Categories);
}
