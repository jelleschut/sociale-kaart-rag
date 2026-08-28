using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SocialeKaartRag.Eval;

public sealed class EvalCase
{
    public required string Id { get; set; }
    public required string Category { get; set; }   // groundedness | refusal | injection | pii | provenance
    public required string Question { get; set; }
    public Expectation Expect { get; set; } = new();
}

public sealed class Expectation
{
    public string? Outcome { get; set; }                 // answered | refused_medical | refused_scope | escalated
    public string? SourceUrlContains { get; set; }       // minstens één geciteerde bron bevat dit
    public string? ForbiddenOutput { get; set; }         // injectie: mag NIET in het antwoord staan
    public string[] PiiTypes { get; set; } = [];         // pii: verwachte geredigeerde typen
    public bool Judge { get; set; }                      // groundedness: LLM-judge inschakelen
}

public static class Cases
{
    public static readonly string[] Categories = ["groundedness", "refusal", "injection", "pii", "provenance"];
    private static readonly IDeserializer Yaml = new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).IgnoreUnmatchedProperties().Build();

    public static List<EvalCase> Load(string path) => Parse(File.ReadAllText(path));
    public static List<EvalCase> Parse(string yaml) => Yaml.Deserialize<List<EvalCase>>(yaml) ?? [];
}
