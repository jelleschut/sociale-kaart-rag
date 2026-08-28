using SocialeKaartRag.Core.SocialMap;

namespace SocialeKaartRag.Core.Tests;

public class TaxonomyTests
{
    [Theory]
    [InlineData("doctors", null, null, null, "gezondheid")]
    [InlineData(null, "pharmacy", null, null, "gezondheid")]
    [InlineData("dentist", "dentist", null, null, "gezondheid")]
    [InlineData("social_facility", null, "assisted_living", null, "wonen")]
    [InlineData("social_facility", null, "nursing_home", null, "wonen")]
    [InlineData("social_facility", null, "shelter", null, "wonen")]
    [InlineData("social_facility", null, "outreach", null, "welzijn")]
    [InlineData("social_facility", null, null, null, "welzijn")]
    [InlineData("community_centre", null, null, null, "welzijn")]
    [InlineData(null, null, null, "ngo", "welzijn")]
    [InlineData(null, "counselling", null, null, "welzijn")]
    [InlineData("social_facility", null, "day_care", null, "mantelzorg")]
    public void Maps_osm_tags(string? amenity, string? healthcare, string? facility, string? office, string expected)
        => Assert.Equal(expected, Taxonomy.FromOsm(amenity, healthcare, facility, office));

    [Theory]
    [InlineData("cafe", null, null, null)]
    [InlineData(null, null, null, "company")]
    [InlineData(null, null, null, null)]
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
    public void Maps_sc_subject_and_keywords(string? subject, string title, string abstractText, string? expected)
        => Assert.Equal(expected, Taxonomy.FromSamenwerkendeCatalogi(subject, title, abstractText));

    [Fact]
    public void Categories_are_the_fixed_six()
        => Assert.Equal(["gezondheid", "werk_inkomen", "wonen", "vervoer", "welzijn", "mantelzorg"], Taxonomy.Categories);
}
