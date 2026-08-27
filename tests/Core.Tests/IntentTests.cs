using System.Text.Json;
using SocialeKaartRag.Core.Policy;

namespace SocialeKaartRag.Core.Tests;

public class IntentTests
{
    [Theory]
    [InlineData("gezondheid", "medical_advice", true, false)]
    [InlineData("out_of_scope", "information", false, true)]
    [InlineData("wonen", "other", false, true)]
    [InlineData("wonen", "find_help", false, false)]
    public void Flags_follow_domain_and_kind(string domain, string kind, bool medical, bool outOfScope)
    {
        var i = new Intent(domain, kind, "social-map");
        Assert.Equal(medical, i.IsMedical);
        Assert.Equal(outOfScope, i.IsOutOfScope);
    }

    [Fact]
    public void Deserializes_model_json()
    {
        var i = JsonSerializer.Deserialize<Intent>("""{"domain":"welzijn","intent":"find_help","corpus":"social-map"}""")!;
        Assert.Equal("welzijn", i.Domain);
        Assert.Equal("find_help", i.Kind);
        Assert.Equal("social-map", i.Corpus);
    }

    [Fact]
    public void Schema_is_valid_json_with_the_three_required_fields()
    {
        using var doc = JsonDocument.Parse(OpenAiIntentClassifier.SchemaJson);
        var required = doc.RootElement.GetProperty("required").EnumerateArray().Select(e => e.GetString()!).ToArray();
        Assert.Equal(["domain", "intent", "corpus"], required);
    }

    [Fact]
    public void Refusal_texts_contain_the_mandatory_referrals()
    {
        Assert.Contains("huisarts", RefusalTexts.Medical);
        Assert.Contains("112", RefusalTexts.Medical);
        Assert.Matches(@"^\d+\.\d+\.\d+$", PolicyVersion.Current);
    }
}
