using SocialeKaartRag.Core.Retrieval;

namespace SocialeKaartRag.Core.Tests;

public class GeoQueryTests
{
    [Fact]
    public void Filter_includes_corpus_and_distance_invariant_culture()
        // SC-producten dragen één gemeente-centroïde, geen locatie; ze horen altijd binnen de eigen
        // gemeente beschikbaar te zijn, dus de geo-filter geldt alleen voor OSM-records (bron:sc ontsnapt eraan).
        => Assert.Equal("corpus eq 'social-map' and (geo.distance(geo, geography'POINT(4.3156 52.0813)') le 5 or tags/any(t: t eq 'bron:sc'))",
            AzureSearchTool.BuildFilter("social-map", new GeoPoint(52.0813, 4.3156), 5));

    [Fact]
    public void Filter_without_optional_parts()
        => Assert.Equal("corpus eq 'social-map'", AzureSearchTool.BuildFilter("social-map", null, 5));

    [Fact]
    public void Category_is_never_a_hard_filter_anymore()
        // ADR-0006: de categorie is een boost via het scoring profile, geen OData-filter. Het filter mag dus
        // onder geen beding nog een category-clausule bevatten.
        => Assert.DoesNotContain("category", AzureSearchTool.BuildFilter("social-map", new GeoPoint(52.0813, 4.3156), 5));

    [Fact]
    public void Attribution_is_read_from_tags()
        => Assert.Equal("© OSM", SearchHit.AttributionFromTags(["gemeente:Den Haag", "attribution:© OSM"]));
}
