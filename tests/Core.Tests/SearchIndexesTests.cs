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
