namespace SocialeKaartRag.Core;

public interface IAskOrchestrator
{
    Task<AskResult> AskAsync(string question, string correlationId, CancellationToken ct = default);
}
