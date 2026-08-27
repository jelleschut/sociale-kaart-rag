using System.Text;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

namespace SocialeKaartRag.Core.Trace;

/// <summary>Eén JSON-regel per request in een append-blob per dag (container 'traces', lifecycle 90 d in Terraform),
/// plus één blob per correlationId voor GET /trace/{id}.</summary>
public sealed class BlobTraceSink(BlobServiceClient blobs) : ITraceSink, ITraceReader
{
    private readonly BlobContainerClient _container = blobs.GetBlobContainerClient("traces");

    public async Task WriteAsync(TraceRecord record, CancellationToken ct = default)
    {
        var line = JsonSerializer.Serialize(record, TraceRecord.JsonOptions) + "\n";
        var bytes = Encoding.UTF8.GetBytes(line);

        var daily = _container.GetAppendBlobClient($"{record.Timestamp:yyyy/MM/dd}.jsonl");
        await daily.CreateIfNotExistsAsync(cancellationToken: ct);
        await daily.AppendBlockAsync(new MemoryStream(bytes), cancellationToken: ct);

        await _container.GetBlobClient($"by-id/{record.CorrelationId}.json")
            .UploadAsync(BinaryData.FromBytes(bytes), overwrite: true, ct);
    }

    public async Task<TraceRecord?> ReadAsync(string correlationId, CancellationToken ct = default)
    {
        try
        {
            var r = await _container.GetBlobClient($"by-id/{correlationId}.json").DownloadContentAsync(ct);
            return JsonSerializer.Deserialize<TraceRecord>(r.Value.Content, TraceRecord.JsonOptions);
        }
        catch (RequestFailedException ex) when (ex.Status == 404) { return null; }
    }
}
