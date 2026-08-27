using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenAI.Chat;
using SocialeKaartRag.Core.Retrieval;

namespace SocialeKaartRag.Core.Generation;

/// <summary>Eén chat-completion, temperature 0, beperkte tokens, JSON-schema (spec §4.4).</summary>
public sealed class OpenAiAnswerGenerator(ChatClient chat) : IAnswerGenerator
{
    public static readonly string SchemaJson = """
        { "type": "object", "additionalProperties": false,
          "properties": {
            "answer": { "type": "array", "items": { "type": "object", "additionalProperties": false,
              "properties": {
                "text": { "type": "string" },
                "kind": { "type": "string", "enum": ["fact","summary"] },
                "citations": { "type": "array", "items": { "type": "string" } } },
              "required": ["text","kind","citations"] } },
            "confidence": { "type": "string", "enum": ["high","low"] },
            "followUp": { "type": ["string","null"] } },
          "required": ["answer","confidence","followUp"] }
        """;

    private static readonly ChatResponseFormat Schema =
        ChatResponseFormat.CreateJsonSchemaFormat("answer", BinaryData.FromString(SchemaJson), jsonSchemaIsStrict: true);

    public async Task<GenerationResult> GenerateAsync(string redactedQuestion, IReadOnlyList<SearchHit> hits, CancellationToken ct = default)
    {
        var user = Prompts.BuildUserTurn(redactedQuestion, hits);
        var completion = await chat.CompleteChatAsync(
            [new SystemChatMessage(Prompts.System), new UserChatMessage(user)],
            new ChatCompletionOptions { ResponseFormat = Schema, Temperature = 0, MaxOutputTokenCount = 600 },
            ct);

        var c = completion.Value;
        if (!string.IsNullOrEmpty(c.Refusal) || c.Content.Count == 0)
            throw new InvalidOperationException("Generatie geweigerd of leeg antwoord van het model.");
        Answer answer;
        try
        {
            answer = JsonSerializer.Deserialize<Answer>(c.Content[0].Text)
                     ?? throw new InvalidOperationException("Generatie leverde geen JSON.");
        }
        catch (JsonException ex) { throw new InvalidOperationException("Generatie leverde geen geldige JSON.", ex); }

        var usage = new GenerationUsage(
            c.Usage.InputTokenCount, c.Usage.OutputTokenCount,
            c.Usage.InputTokenDetails?.CachedTokenCount ?? 0, c.Model);
        var promptHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Prompts.System + "\n" + user)));
        return new GenerationResult(answer, usage, promptHash);
    }
}
