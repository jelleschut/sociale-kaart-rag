using System.Text.Json;
using System.Text.Json.Serialization;
using OpenAI.Chat;

namespace SocialeKaartRag.Core.Policy;

public sealed record Intent(
    [property: JsonPropertyName("domain")] string Domain,   // gezondheid|werk_inkomen|wonen|vervoer|welzijn|mantelzorg|platform_kennis|out_of_scope
    [property: JsonPropertyName("intent")] string Kind,     // find_help|information|medical_advice|other
    [property: JsonPropertyName("corpus")] string Corpus)   // kb|social-map
{
    public bool IsMedical => Kind == "medical_advice";
    public bool IsOutOfScope => Domain == "out_of_scope" || Kind == "other";
}

public interface IIntentClassifier
{
    Task<Intent> ClassifyAsync(string redactedQuestion, CancellationToken ct = default);
}

/// <summary>Eén goedkope model-call met JSON-schema-output (spec §4.3 stap 2).</summary>
public sealed class OpenAiIntentClassifier(ChatClient chat) : IIntentClassifier
{
    public const string SystemPrompt =
        """
        Je classificeert vragen voor een sociale-kaart-gids. Antwoord uitsluitend met JSON.
        domain: gezondheid | werk_inkomen | wonen | vervoer | welzijn | mantelzorg | platform_kennis | out_of_scope
          - platform_kennis = vragen over de interne kennisbank (tools, runbooks, beslissingen: sops, age, gitlab, kubernetes, argo, ansible, terraform, prometheus, haven, ...)
        intent: find_help | information | medical_advice | other
          - medical_advice = vraagt om diagnose, triage, inschatting van klachten of behandeladvies
          - find_help = zoekt een organisatie, dienst of loket; information = wil uitleg over een regeling of onderwerp
        corpus: social-map voor sociale-kaart-domeinen; kb voor platform_kennis
        """; // platform_kennis/kb = POC-corpus, verdwijnt na plan 2

    public static readonly string SchemaJson = """
        { "type": "object", "additionalProperties": false,
          "properties": {
            "domain": { "type": "string", "enum": ["gezondheid","werk_inkomen","wonen","vervoer","welzijn","mantelzorg","platform_kennis","out_of_scope"] },
            "intent": { "type": "string", "enum": ["find_help","information","medical_advice","other"] },
            "corpus": { "type": "string", "enum": ["kb","social-map"] } },
          "required": ["domain","intent","corpus"] }
        """;

    private static readonly ChatResponseFormat Schema =
        ChatResponseFormat.CreateJsonSchemaFormat("intent", BinaryData.FromString(SchemaJson), jsonSchemaIsStrict: true);

    public async Task<Intent> ClassifyAsync(string redactedQuestion, CancellationToken ct = default)
    {
        var completion = await chat.CompleteChatAsync(
            [new SystemChatMessage(SystemPrompt), new UserChatMessage(redactedQuestion)],
            new ChatCompletionOptions { ResponseFormat = Schema, Temperature = 0, MaxOutputTokenCount = 60 },
            ct);
        return JsonSerializer.Deserialize<Intent>(completion.Value.Content[0].Text)
               ?? throw new InvalidOperationException("Classificatie leverde geen JSON.");
    }
}
