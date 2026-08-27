using SocialeKaartRag.Core.Retrieval;

namespace SocialeKaartRag.Core.Generation;

public sealed record GenerationUsage(int TokensIn, int TokensOut, int TokensCached, string Model);
public sealed record GenerationResult(Answer Answer, GenerationUsage Usage, string PromptHash);

public interface IAnswerGenerator
{
    Task<GenerationResult> GenerateAsync(string redactedQuestion, IReadOnlyList<SearchHit> hits, CancellationToken ct = default);
}
