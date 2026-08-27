namespace SocialeKaartRag.Core.Trace;

/// <summary>Contract: implementaties horen niet te gooien (zie CompositeTraceSink); AskOrchestrator vangt als vangrail toch af.</summary>
public interface ITraceSink
{
    Task WriteAsync(TraceRecord record, CancellationToken ct = default);
}

public interface ITraceReader
{
    Task<TraceRecord?> ReadAsync(string correlationId, CancellationToken ct = default);
}
