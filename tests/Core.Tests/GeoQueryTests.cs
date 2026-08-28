using SocialeKaartRag.Core.Retrieval;

namespace SocialeKaartRag.Core.Tests;

public class GeoQueryTests
{
    [Fact]
    public void Filter_includes_corpus_category_and_distance_invariant_culture()
        // SC-producten dragen één gemeente-centroïde, geen locatie; ze horen altijd binnen de eigen
        // gemeente beschikbaar te zijn, dus de geo-filter geldt alleen voor OSM-records (bron:sc ontsnapt eraan).
        => Assert.Equal("corpus eq 'social-map' and category eq 'welzijn' and (geo.distance(geo, geography'POINT(4.3156 52.0813)') le 5 or tags/any(t: t eq 'bron:sc'))",
            AzureSearchTool.BuildFilter("social-map", "welzijn", new GeoPoint(52.0813, 4.3156), 5));

    [Fact]
    public void Filter_without_optional_parts()
        => Assert.Equal("corpus eq 'kb'", AzureSearchTool.BuildFilter("kb", null, null, 5));

    [Fact]
    public void Category_quotes_are_escaped()
        => Assert.Contains("category eq 'a''b'", AzureSearchTool.BuildFilter("kb", "a'b", null, 5));

    [Fact]
    public void Attribution_is_read_from_tags()
        => Assert.Equal("© OSM", SearchHit.AttributionFromTags(["gemeente:Den Haag", "attribution:© OSM"]));
}
