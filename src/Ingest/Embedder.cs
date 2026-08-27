using OpenAI.Embeddings;

namespace SocialeKaartRag.Ingest;

/// <summary>Batcht embeddings (16 per call) met exponentiële backoff op 429.</summary>
public sealed class Embedder(EmbeddingClient client)
{
    public async Task<List<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        var result = new List<float[]>(texts.Count);
        foreach (var batch in texts.Chunk(16))
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    var resp = await client.GenerateEmbeddingsAsync(batch, cancellationToken: ct);
                    result.AddRange(resp.Value.Select(e => e.ToFloats().ToArray()));
                    break;
                }
                catch (System.ClientModel.ClientResultException ex) when (ex.Status == 429 && attempt < 6)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
                }
            }
        }
        return result;
    }
}
