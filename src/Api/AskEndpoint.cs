using Microsoft.AspNetCore.RateLimiting;
using SocialeKaartRag.Core;
using SocialeKaartRag.Core.Trace;

namespace SocialeKaartRag.Api;

public sealed record AskRequest(string? Question);

public static class AskEndpoint
{
    public const int MaxQuestionLength = 1000;

    public static void MapAsk(this WebApplication app)
    {
        app.MapPost("/ask", async (AskRequest req, IAskOrchestrator orchestrator, HttpContext http, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Question) || req.Question.Length > MaxQuestionLength)
                return Results.BadRequest(new { error = $"question is verplicht, max {MaxQuestionLength} tekens" });

            var correlationId = Guid.NewGuid().ToString("n");
            http.Items["CorrelationId"] = correlationId;
            http.Response.Headers["X-Correlation-Id"] = correlationId;
            var result = await orchestrator.AskAsync(req.Question, correlationId, ct);
            return Results.Ok(new
            {
                correlationId = result.CorrelationId,
                outcome = ToSnake(result.Outcome),
                message = result.Message,
                answer = result.Answer?.Items.Select(i => new { text = i.Text, kind = i.Kind, citations = i.Citations }),
                confidence = result.Answer?.Confidence,
                followUp = result.Answer?.FollowUp,
                sources = result.Sources.Select(s => new { id = s.Id, sourceId = s.SourceId, url = s.Url, heading = s.Heading, lastVerified = s.LastVerified, attribution = s.Attribution }),
                policyVersion = result.PolicyVersion,
            });
        }).RequireRateLimiting("ask");

        app.MapPost("/ask/fragment", async (HttpRequest request, IAskOrchestrator orchestrator, HttpContext http, CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
                return Results.Content("<p class=\"message\">Verwacht een formulier (application/x-www-form-urlencoded).</p>", "text/html; charset=utf-8", statusCode: 400);
            var form = await request.ReadFormAsync(ct);
            var question = form["question"].ToString();
            if (string.IsNullOrWhiteSpace(question) || question.Length > MaxQuestionLength)
                return Results.Content("<p class=\"message\">Stel een vraag van maximaal 1000 tekens.</p>", "text/html; charset=utf-8", statusCode: 400);
            var correlationId = Guid.NewGuid().ToString("n");
            http.Items["CorrelationId"] = correlationId;
            http.Response.Headers["X-Correlation-Id"] = correlationId;
            var result = await orchestrator.AskAsync(question, correlationId, ct);
            return Results.Content(AskHtml.Render(result), "text/html; charset=utf-8");
        }).DisableAntiforgery() // defensief: er zijn geen cookies/sessies, dus geen CSRF-oppervlak; antiforgery is niet geregistreerd
          .RequireRateLimiting("ask");

        app.MapGet("/trace/{id}", async (string id, ITraceReader reader, CancellationToken ct) =>
        {
            if (id.Length != 32 || !id.All(char.IsAsciiHexDigitLower)) return Results.BadRequest();
            var t = await reader.ReadAsync(id, ct);
            return t is null ? Results.NotFound() : Results.Json(t, TraceRecord.JsonOptions);
        });
    }

    private static string ToSnake(TraceOutcome o) => o switch
    {
        TraceOutcome.Answered => "answered", TraceOutcome.RefusedMedical => "refused_medical", TraceOutcome.RefusedScope => "refused_scope",
        TraceOutcome.Escalated => "escalated", _ => "error",
    };
}
