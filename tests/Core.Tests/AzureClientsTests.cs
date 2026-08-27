using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SocialeKaartRag.Core;

namespace SocialeKaartRag.Core.Tests;

public class AzureClientsTests
{
    [Fact]
    public void Binds_azure_section_with_defaults()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Azure:OpenAiEndpoint"] = "https://oai.example/",
            ["Azure:SearchEndpoint"] = "https://srch.example/",
            ["Azure:StorageAccountUrl"] = "https://st.example/",
        }).Build();
        var sp = new ServiceCollection().AddAzureClients(config).BuildServiceProvider();
        var opts = sp.GetRequiredService<AzureOptions>();
        Assert.Equal("gpt-4.1-mini", opts.ChatDeployment);
        Assert.Equal("text-embedding-3-small", opts.EmbeddingDeployment);
    }

    [Fact]
    public void Missing_section_throws_clear_error()
    {
        var config = new ConfigurationBuilder().Build();
        var ex = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddAzureClients(config));
        Assert.Contains("Azure", ex.Message);
    }
}
