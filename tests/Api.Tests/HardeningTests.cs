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
    public async Task Rate_limit_returns_429_with_retry_after_when_the_window_is_exhausted()
    {
        var c = f.CreateClient();
        HttpResponseMessage last = null!;
        for (var i = 0; i < 25; i++) last = await c.PostAsync("/ask/fragment", new FormUrlEncodedContent([new("question", "hulp")]));
        Assert.Equal(HttpStatusCode.TooManyRequests, last.StatusCode);
        Assert.Equal("60", last.Headers.GetValues("Retry-After").Single());
    }

    [Fact]
    public async Task Rate_limiting_partitions_by_forwarded_client_ip_behind_ingress()
    {
        // Container Apps-ingress zit ervoor: zonder X-Forwarded-For valt de partitionering terug op een
        // gedeelde "anon"-bucket, daarom hier een verse factory (los van de gedeelde f-fixture).
        using var factory = new ApiFactory();
        var clientA = factory.CreateClient();
        var clientB = factory.CreateClient();

        HttpStatusCode lastA = HttpStatusCode.OK;
        for (var i = 0; i < 20; i++) lastA = (await PostAsFragment(clientA, "203.0.113.10")).StatusCode;
        Assert.Equal(HttpStatusCode.OK, lastA);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await PostAsFragment(clientA, "203.0.113.10")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostAsFragment(clientB, "203.0.113.20")).StatusCode);
    }

    private static Task<HttpResponseMessage> PostAsFragment(HttpClient client, string forwardedFor)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/ask/fragment")
        {
            Content = new FormUrlEncodedContent([new("question", "hulp")]),
        };
        req.Headers.Add("X-Forwarded-For", forwardedFor);
        return client.SendAsync(req);
    }

    [Fact]
    public async Task Unhandled_exception_returns_problem_details_without_stack_trace()
    {
        using var boom = new ApiFactory().WithWebHostBuilder(b => b.ConfigureServices(s => { s.RemoveAll<IAskOrchestrator>(); s.AddSingleton<IAskOrchestrator, ThrowingOrchestrator>(); }));
        var resp = await boom.CreateClient().PostAsJsonAsync("/ask", new { question = "x" });
        Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
        var correlationId = resp.Headers.GetValues("X-Correlation-Id").Single();
        Assert.False(string.IsNullOrEmpty(correlationId));
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":500", body);
        Assert.Contains($"\"correlationId\":\"{correlationId}\"", body);
        Assert.DoesNotContain("at SocialeKaartRag", body);
        Assert.DoesNotContain("boom-detail", body);
    }

    private sealed class ThrowingOrchestrator : IAskOrchestrator
    {
        public Task<AskResult> AskAsync(string q, string id, CancellationToken ct = default) => throw new InvalidOperationException("boom-detail");
    }
}
