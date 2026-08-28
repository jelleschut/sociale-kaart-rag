using SocialeKaartRag.Core.Retrieval;

namespace SocialeKaartRag.Core.Tests;

public class GeocodingTests
{
    [Theory]
    [InlineData("welke hulp is er in 2511CV?", "2511CV")]
    [InlineData("ik woon in 2511 cv, waar kan ik terecht", "2511CV")]
    [InlineData("hulp bij schulden", null)]
    [InlineData("in 2023 is er 12 procent", null)]
    public void Postcode_is_extracted_from_question(string q, string? expected) => Assert.Equal(expected, PostcodeDetector.Find(q));

    [Fact]
    public void Pdok_response_is_parsed_to_point()
    {
        var json = """{"response":{"numFound":1,"docs":[{"type":"postcode","centroide_ll":"POINT(4.31557609 52.08130867)"}]}}""";
        var p = PdokGeocoder.ParsePoint(json)!;
        Assert.Equal(52.0813, p.Lat, 4); Assert.Equal(4.3156, p.Lon, 4);
    }

    [Fact]
    public void Empty_pdok_response_is_null()
        => Assert.Null(PdokGeocoder.ParsePoint("""{"response":{"numFound":0,"docs":[]}}"""));
}
