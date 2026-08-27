using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;

namespace SocialeKaartRag.Core.Trace;

public sealed class AppInsightsTraceSink(TelemetryClient telemetry) : ITraceSink
{
    public Task WriteAsync(TraceRecord r, CancellationToken ct = default)
    {
        var evt = new EventTelemetry("rag.request");
        evt.Properties["correlationId"] = r.CorrelationId;
        evt.Properties["policyVersion"] = r.PolicyVersion;
        evt.Properties["model"] = r.Model ?? "";
        evt.Properties["intent"] = r.Intent ?? "";
        evt.Properties["domain"] = r.Domain ?? "";
        evt.Properties["outcome"] = r.Outcome.ToString();
        evt.Properties["piiRedacted"] = r.PiiRedacted.ToString();
        evt.Properties["refusalReason"] = r.RefusalReason ?? "";
        // Microsoft.ApplicationInsights 3.x kent EventTelemetry.Metrics niet meer (verwijderd t.o.v. 2.x);
        // numerieke velden gaan daarom als string mee in Properties (customDimensions), nog steeds query-baar in App Insights.
        evt.Properties["tokensIn"] = r.TokensIn.ToString();
        evt.Properties["tokensOut"] = r.TokensOut.ToString();
        evt.Properties["tokensCached"] = r.TokensCached.ToString();
        evt.Properties["estimatedCostEur"] = r.EstimatedCostEur.ToString("F6");
        evt.Properties["latencyMs"] = r.LatencyMs.ToString();
        evt.Properties["retrieved"] = r.RetrievedChunkIds.Length.ToString();
        telemetry.TrackEvent(evt);
        return Task.CompletedTask;
    }
}

/// <summary>Schrijft naar alle sinks; een falende sink mag het antwoord niet blokkeren.</summary>
public sealed class CompositeTraceSink(IEnumerable<ITraceSink> sinks, ILogger<CompositeTraceSink> log) : ITraceSink
{
    public async Task WriteAsync(TraceRecord record, CancellationToken ct = default)
    {
        foreach (var s in sinks)
        {
            try { await s.WriteAsync(record, ct); }
            catch (Exception ex) { log.LogError(ex, "trace-sink {Sink} faalde voor {CorrelationId}", s.GetType().Name, record.CorrelationId); }
        }
    }
}
