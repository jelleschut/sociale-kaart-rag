using System.Threading.RateLimiting;
using Azure.Search.Documents.Indexes;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using OpenAI.Chat;
using OpenAI.Embeddings;
using SocialeKaartRag.Api;
using SocialeKaartRag.Core;
using SocialeKaartRag.Core.Generation;
using SocialeKaartRag.Core.Policy;
using SocialeKaartRag.Core.Retrieval;
using SocialeKaartRag.Core.Trace;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAzureClients(builder.Configuration);

// Per-client-IP rate limiting op de bevraagbare endpoints (/ask, /ask/fragment); /healthz en statische
// bestanden blijven ongelimiteerd omdat dit een named policy is, geen GlobalLimiter.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("ask", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.OnRejected = (ctx, ct) =>
    {
        ctx.HttpContext.Response.Headers.RetryAfter = "60";
        return ValueTask.CompletedTask;
    };
});

builder.Services.AddSingleton<IIntentClassifier>(sp => new OpenAiIntentClassifier(sp.GetRequiredService<ChatClient>()));
builder.Services.AddSingleton<IAnswerGenerator>(sp => new OpenAiAnswerGenerator(sp.GetRequiredService<ChatClient>()));
builder.Services.AddSingleton<ISearchTool>(sp => new AzureSearchTool(sp.GetRequiredService<SearchIndexClient>(), sp.GetRequiredService<EmbeddingClient>(), SearchIndexes.SocialMap, SearchIndexes.SocialMapCorpus));

builder.Services.AddHttpClient("pdok", c =>
{
    c.Timeout = TimeSpan.FromSeconds(5);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("sociale-kaart-rag/1.0 (+https://github.com/jelleschut/sociale-kaart-rag)");
});
builder.Services.AddSingleton<IGeocoder>(sp => new PdokGeocoder(sp.GetRequiredService<IHttpClientFactory>().CreateClient("pdok")));

builder.Services.AddSingleton<BlobTraceSink>();
builder.Services.AddSingleton<ITraceReader>(sp => sp.GetRequiredService<BlobTraceSink>());
var aiConn = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
builder.Services.AddSingleton<ITraceSink>(sp =>
{
    var sinks = new List<ITraceSink> { sp.GetRequiredService<BlobTraceSink>() };
    if (!string.IsNullOrEmpty(aiConn))
    {
        var cfg = TelemetryConfiguration.CreateDefault();
        cfg.ConnectionString = aiConn;
        var telemetry = new TelemetryClient(cfg);
        sinks.Add(new AppInsightsTraceSink(telemetry));
        // Scale-to-zero: gebufferde telemetrie flushen vóór de container stopt.
        sp.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.Register(() => telemetry.Flush());
    }
    return new CompositeTraceSink(sinks, sp.GetRequiredService<ILogger<CompositeTraceSink>>());
});
builder.Services.AddSingleton<IAskOrchestrator, AskOrchestrator>();

var app = builder.Build();

// Container Apps-ingress zit ervoor; alleen de laatste hop wordt vertrouwd (ForwardLimit 1).
var fwd = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    ForwardLimit = 1,
};
fwd.KnownIPNetworks.Clear();
fwd.KnownProxies.Clear();
app.UseForwardedHeaders(fwd);

// Vangt ook fouten uit latere middleware af en geeft nooit een stack trace terug
// (ook niet in Development — expliciete registratie voorkomt dat de developer exception page dit overneemt).
app.UseExceptionHandler(e => e.Run(async ctx =>
{
    var correlationId = ctx.Items["CorrelationId"] as string ?? ctx.TraceIdentifier;
    ctx.Response.StatusCode = 500;
    ctx.Response.ContentType = "application/problem+json";
    ctx.Response.Headers["X-Correlation-Id"] = correlationId;
    await ctx.Response.WriteAsJsonAsync(new
    {
        type = "about:blank",
        title = "Interne fout",
        status = 500,
        correlationId,
    });
}));

// Beveiligingsheaders vóór static files, zodat ook `/` ze krijgt.
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["Content-Security-Policy"] = "default-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

app.UseRateLimiter();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapAsk();
app.MapGet("/healthz", () => Results.Ok(new { status = "ok", policyVersion = PolicyVersion.Current }));
app.Run();

public partial class Program { }
