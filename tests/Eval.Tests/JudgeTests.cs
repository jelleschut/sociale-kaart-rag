using SocialeKaartRag.Eval;

namespace SocialeKaartRag.Eval.Tests;

public class JudgeTests
{
    [Fact]
    public void Judge_prompt_contains_sources_and_claims_and_schema_is_strict()
    {
        var user = OpenAiGroundednessJudge.BuildUserTurn(["bron één tekst"], ["claim a", "claim b"]);
        Assert.Contains("<source id=\"1\">bron één tekst</source>", user);
        Assert.Contains("1. claim a", user);
        using var doc = System.Text.Json.JsonDocument.Parse(OpenAiGroundednessJudge.SchemaJson);
        Assert.False(doc.RootElement.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(["grounded", "reason"], doc.RootElement.GetProperty("required").EnumerateArray().Select(e => e.GetString()!).ToArray());
    }

    [Fact]
    public void Verdict_json_is_parsed()
    {
        var v = OpenAiGroundednessJudge.ParseVerdict("""{"grounded":false,"reason":"claim 2 staat niet in de bronnen"}""");
        Assert.False(v.Grounded); Assert.Contains("claim 2", v.Reason);
    }
}
