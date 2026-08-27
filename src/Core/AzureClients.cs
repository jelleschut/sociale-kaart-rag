using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents.Indexes;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SocialeKaartRag.Core;

public sealed class AzureOptions
{
    public required string OpenAiEndpoint { get; init; }
    public required string SearchEndpoint { get; init; }
    public required string StorageAccountUrl { get; init; }
    public string ChatDeployment { get; init; } = "gpt-4.1-mini";
    public string EmbeddingDeployment { get; init; } = "text-embedding-3-small";
}

public static class AzureClients
{
    /// <summary>Registreert alle Azure-clients op één DefaultAzureCredential; geen keys (spec §5).</summary>
    public static IServiceCollection AddAzureClients(this IServiceCollection services, IConfiguration config)
    {
        var opts = config.GetSection("Azure").Get<AzureOptions>()
                   ?? throw new InvalidOperationException("Sectie Azure ontbreekt (OpenAiEndpoint/SearchEndpoint/StorageAccountUrl).");
        var credential = new DefaultAzureCredential();

        services.AddSingleton(opts);
        services.AddSingleton(new AzureOpenAIClient(new Uri(opts.OpenAiEndpoint), credential));
        services.AddSingleton(new SearchIndexClient(new Uri(opts.SearchEndpoint), credential));
        services.AddSingleton(new BlobServiceClient(new Uri(opts.StorageAccountUrl), credential));
        services.AddSingleton(sp => sp.GetRequiredService<AzureOpenAIClient>().GetEmbeddingClient(opts.EmbeddingDeployment));
        services.AddSingleton(sp => sp.GetRequiredService<AzureOpenAIClient>().GetChatClient(opts.ChatDeployment));
        return services;
    }
}
