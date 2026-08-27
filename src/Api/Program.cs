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
foreach (var corpus in new[] { SearchIndexes.Kb, SearchIndexes.SocialMap })
    builder.Services.AddSingleton<ISearchTool>(sp => new AzureSearchTool(sp.GetRequiredService<SearchIndexClient>(), sp.GetRequiredService<EmbeddingClient>(), corpus));

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
