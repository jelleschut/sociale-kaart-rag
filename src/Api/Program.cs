using Azure.Search.Documents.Indexes;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
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

builder.Services.AddSingleton<IIntentClassifier>(sp => new OpenAiIntentClassifier(sp.GetRequiredService<ChatClient>()));
builder.Services.AddSingleton<IAnswerGenerator>(sp => new OpenAiAnswerGenerator(sp.GetRequiredService<ChatClient>()));
builder.Services.AddSingleton<ISearchTool>(sp => new AzureSearchTool(sp.GetRequiredService<SearchIndexClient>(), sp.GetRequiredService<EmbeddingClient>(), SearchIndexes.SocialMap));

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
builder.Services.AddSingleton<AskOrchestrator>();

var app = builder.Build();
app.MapAsk();
app.MapGet("/healthz", () => Results.Ok(new { status = "ok", policyVersion = PolicyVersion.Current }));
app.Run();
