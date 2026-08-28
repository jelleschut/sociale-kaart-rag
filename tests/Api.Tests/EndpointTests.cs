using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SocialeKaartRag.Core;
using SocialeKaartRag.Core.Generation;
using SocialeKaartRag.Core.Trace;

namespace SocialeKaartRag.Api.Tests;

public sealed class FakeOrchestrator : IAskOrchestrator
{
    public Task<AskResult> AskAsync(string question, string correlationId, CancellationToken ct = default) =>
        Task.FromResult(question.Contains("koorts")
            ? new AskResult(correlationId, TraceOutcome.RefusedMedical, Core.Policy.RefusalTexts.Medical, null, [], "1.0.1")
            : new AskResult(correlationId, TraceOutcome.Answered, null, new Answer([new("Antwoord", "fact", ["c1"])], "high", null),
                [new SourceRef("c1", "s", "https://example.org/x", "Bron", null, "© test")], "1.0.1"));
}

public class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("Azure:OpenAiEndpoint", "https://oai.example/");
        builder.UseSetting("Azure:SearchEndpoint", "https://srch.example/");
        builder.UseSetting("Azure:StorageAccountUrl", "https://st.example/");
        builder.ConfigureServices(s => { s.RemoveAll<IAskOrchestrator>(); s.AddSingleton<IAskOrchestrator, FakeOrchestrator>(); });
    }
}

public class EndpointTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Index_page_is_served()
    {
        var html = await f.CreateClient().GetStringAsync("/");
        Assert.Contains("Sociale kaart", html);
        Assert.Contains("hx-post=\"/ask/fragment\"", html);
    }

    [Fact]
    public async Task Fragment_returns_rendered_html_and_correlation_header()
    {
        var resp = await f.CreateClient().PostAsync("/ask/fragment", new FormUrlEncodedContent([new("question", "waar is hulp?")]));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.True(resp.Headers.Contains("X-Correlation-Id"));
        var html = await resp.Content.ReadAsStringAsync();
        Assert.Contains("badge-feit", html);
        Assert.Contains("© test", html);
    }

    [Fact]
    public async Task Fragment_shows_medical_refusal()
    {
        var resp = await f.CreateClient().PostAsync("/ask/fragment", new FormUrlEncodedContent([new("question", "ik heb koorts")]));
        Assert.Contains("huisarts", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Empty_question_is_400()
    {
        var resp = await f.CreateClient().PostAsync("/ask/fragment", new FormUrlEncodedContent([new("question", " ")]));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Fragment_with_json_body_is_400_not_500()
    {
        var resp = await f.CreateClient().PostAsJsonAsync("/ask/fragment", new { question = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Json_ask_still_works()
    {
        var resp = await f.CreateClient().PostAsJsonAsync("/ask", new { question = "waar is hulp?" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Contains("\"outcome\":\"answered\"", await resp.Content.ReadAsStringAsync());
    }
}
