using Azure.Search.Documents.Indexes.Models;
using SocialeKaartRag.Core.Chunks;
using SocialeKaartRag.Core.Retrieval;

namespace SocialeKaartRag.Core.Tests;

public class SearchIndexesTests
{
    [Fact]
    public void Index_has_key_vector_and_filter_fields()
    {
        var idx = SearchIndexes.Define("social-map");
        Assert.Equal("social-map", idx.Name);
        Assert.Contains(idx.Fields, f => f.Name == "id" && f.IsKey == true);
        Assert.Contains(idx.Fields, f => f.Name == "vector" && f.VectorSearchDimensions == SearchIndexes.EmbeddingDimensions);
        Assert.Contains(idx.Fields, f => f.Name == "corpus" && f.IsFilterable == true);
        Assert.Contains(idx.Fields, f => f.Name == "geo" && f.Type == SearchFieldDataType.GeographyPoint);
    }

    [Fact]
    public void Index_name_is_v2_and_corpus_name_is_stable()
    {
        // De fysieke index versionert mee met het schema; de logische corpusnaam (chunk.corpus, intent.corpus,
        // filterwaarde) mag daar nooit in meebewegen — anders sluit het harde corpus-filter alles uit.
        Assert.Equal("social-map-v2", SearchIndexes.SocialMap);
        Assert.Equal("social-map", SearchIndexes.SocialMapCorpus);
    }

    [Fact]
    public void Index_has_categories_collection_and_category_boost_profile()
    {
        var idx = SearchIndexes.Define(SearchIndexes.SocialMap);
        var categories = Assert.Single(idx.Fields, f => f.Name == "categories");
        Assert.Equal(SearchFieldDataType.Collection(SearchFieldDataType.String), categories.Type);
        Assert.True(categories.IsFilterable);
        Assert.True(categories.IsFacetable);
        Assert.Contains(idx.Fields, f => f.Name == "category"); // primaire categorie blijft, voor attributie/logging

        var profile = Assert.Single(idx.ScoringProfiles);
        Assert.Equal(SearchIndexes.CategoryBoostProfile, profile.Name);
        var fn = Assert.IsType<TagScoringFunction>(Assert.Single(profile.Functions));
        Assert.Equal("categories", fn.FieldName);
        Assert.Equal(SearchIndexes.CategoryBoost, fn.Boost);
        Assert.Equal(SearchIndexes.CategoryBoostParameter, fn.Parameters.TagsParameter);
        Assert.Equal(ScoringFunctionInterpolation.Constant, fn.Interpolation);
    }

    [Fact]
    public void Dto_carries_all_categories_and_falls_back_to_the_primary()
    {
        var c = new Chunk
        {
            Id = "x", Corpus = "social-map", Source = "s", SourceId = "1", RetrievedAt = DateTimeOffset.UnixEpoch,
            ContentHash = "h", Text = "t", Category = "werk_inkomen", Categories = ["werk_inkomen", "welzijn"],
        };
        Assert.Equal(["werk_inkomen", "welzijn"], SearchDocumentDto.From(c, []).categories);
        // Een chunk zonder expliciete set (canary, oudere bron) mag niet zonder boostveld in de index komen.
        Assert.Equal(["werk_inkomen"], SearchDocumentDto.From(c with { Categories = [] }, []).categories);
        Assert.Empty(SearchDocumentDto.From(c with { Category = null, Categories = [] }, []).categories);
    }

    [Fact]
    public void Dto_maps_chunk_and_geo_point()
    {
        var c = new Chunk { Id = "x", Corpus = "social-map", Source = "s", SourceId = "1", RetrievedAt = DateTimeOffset.UnixEpoch, ContentHash = "h", Text = "t", Lat = 52.08, Lon = 4.31 };
        var d = SearchDocumentDto.From(c, new float[] { 0.1f });
        Assert.Equal("x", d.id);
        Assert.NotNull(d.geo);
        Assert.Single(d.vector!);
        Assert.Null(SearchDocumentDto.From(c with { Lat = null }, []).geo);
    }
}
