using System.Linq;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SocialeKaartRag.Core;
using SocialeKaartRag.Core.Trace;

namespace SocialeKaartRag.Api.Tests;

public class HardeningTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Security_headers_are_present()
    {
        var resp = await f.CreateClient().GetAsync("/");
        Assert.Equal("default-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'", resp.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("nosniff", resp.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", resp.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", resp.Headers.GetValues("Referrer-Policy").Single());
    }

    [Fact]
    public async Task Rate_limit_returns_429_after_the_window_is_exhausted()
    {
        var c = f.CreateClient();
        HttpStatusCode last = HttpStatusCode.OK;
        for (var i = 0; i < 25; i++) last = (await c.PostAsync("/ask/fragment", new FormUrlEncodedContent([new("question", "hulp")]))).StatusCode;
        Assert.Equal(HttpStatusCode.TooManyRequests, last);
    }

    [Fact]
    public async Task Unhandled_exception_returns_problem_details_without_stack_trace()
    {
        using var boom = new ApiFactory().WithWebHostBuilder(b => b.ConfigureServices(s => { s.RemoveAll<IAskOrchestrator>(); s.AddSingleton<IAskOrchestrator, ThrowingOrchestrator>(); }));
        var resp = await boom.CreateClient().PostAsJsonAsync("/ask", new { question = "x" });
        Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":500", body);
        Assert.DoesNotContain("at SocialeKaartRag", body);
        Assert.DoesNotContain("boom-detail", body);
    }

    private sealed class ThrowingOrchestrator : IAskOrchestrator
    {
        public Task<AskResult> AskAsync(string q, string id, CancellationToken ct = default) => throw new InvalidOperationException("boom-detail");
    }
}
