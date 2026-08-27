namespace SocialeKaartRag.Core.Trace;

public interface ITraceSink
{
    Task WriteAsync(TraceRecord record, CancellationToken ct = default);
}

public interface ITraceReader
{
    Task<TraceRecord?> ReadAsync(string correlationId, CancellationToken ct = default);
}
