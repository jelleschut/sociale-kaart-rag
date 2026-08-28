# Sociale-kaart RAG — plan 4: htmx-pagina, ADR's en README (spec-stap 7)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Een toonbare live-URL (spec §4.7): één htmx-pagina met vraag, antwoord met badges *feit*/*samenvatting*, citaties met bronlink en attributie, zichtbare correlation-id en de weigerings-/escalatieteksten — plus de documentatie uit spec §9 (ADR-0001, ADR-0004, `docs/traceability.md`, README met architectuurplaat en "wat dit wel/niet bewijst").

**Architecture:** De API serveert een statische `index.html` (htmx gevendord in `wwwroot`, geen CDN) en een nieuw endpoint `POST /ask/fragment` dat dezelfde orchestrator aanroept en een server-gerenderd HTML-fragment teruggeeft (pure renderer, HTML-encoded, unit-getest). Een `IAskOrchestrator`-interface maakt de endpoints testbaar met een `WebApplicationFactory` en fakes. Productie-hardening die een publieke URL nodig heeft: globale exception-handler (ProblemDetails, geen lek), per-IP rate limiting (kostenrem), security-headers (CSP `default-src 'self'`). Geen sessies, cookies of accounts.

**Tech Stack:** bestaand + `htmx` 2.x (gevendord), `Microsoft.AspNetCore.RateLimiting` (ingebouwd), `Microsoft.AspNetCore.Mvc.Testing` (Api-tests), mermaid in README.

**Gate (spec §10 stap 7):** live-URL toonbaar: `/` rendert de pagina, een vraag geeft een gecieerd antwoord met badges/attributie/correlation-id, een medische vraag toont de weigering.

---

## Bestandsstructuur

```
src/Core/IAskOrchestrator.cs                # interface (Task 1)
src/Core/AskOrchestrator.cs                 # implementeert IAskOrchestrator
src/Core/Trace/TraceRecord.cs               # + ModelVersion (Task 2, follow-up #1)
src/Api/AskEndpoint.cs                      # gebruikt IAskOrchestrator; + POST /ask/fragment
src/Api/AskHtml.cs                          # pure renderer: AskResult → HTML-fragment (Task 1)
src/Api/Program.cs                          # static files, rate limiter, exception handler, headers
src/Api/wwwroot/index.html, wwwroot/htmx.min.js, wwwroot/site.css
tests/Api.Tests/SocialeKaartRag.Api.Tests.csproj   # WebApplicationFactory + fakes
tests/Api.Tests/{AskHtmlTests,EndpointTests,HardeningTests}.cs
docs/adr/0001-application-owned-orchestration.md
docs/adr/0004-guardrails-buiten-het-model.md
docs/traceability.md
README.md                                   # herschreven
.github/workflows/deploy.yml                # smoke ook op `/`
```

---

### Task 1: IAskOrchestrator, HTML-renderer en `POST /ask/fragment` + pagina

**Files:** `src/Core/IAskOrchestrator.cs`, `src/Core/AskOrchestrator.cs` (implements), `src/Api/AskHtml.cs`, `src/Api/AskEndpoint.cs`, `src/Api/Program.cs`, `src/Api/wwwroot/*`, `tests/Api.Tests/*`

- [x] **Step 1: Interface**
```csharp
namespace SocialeKaartRag.Core;

public interface IAskOrchestrator
{
    Task<AskResult> AskAsync(string question, string correlationId, CancellationToken ct = default);
}
```
`public sealed class AskOrchestrator(...) : IAskOrchestrator`. In `Program.cs`: `builder.Services.AddSingleton<IAskOrchestrator, AskOrchestrator>();` (en `AskOrchestrator` zelf niet meer los registreren). `AskEndpoint` neemt `IAskOrchestrator`.

- [x] **Step 2: Test-project**
```powershell
dotnet new xunit -n SocialeKaartRag.Api.Tests -o tests/Api.Tests
Remove-Item tests/Api.Tests/UnitTest1.cs
dotnet sln SocialeKaartRag.slnx add tests/Api.Tests
dotnet add tests/Api.Tests reference src/Api
dotnet add tests/Api.Tests package Microsoft.AspNetCore.Mvc.Testing
```
`src/Api/SocialeKaartRag.Api.csproj`: `<InternalsVisibleTo Include="SocialeKaartRag.Api.Tests" />` is niet nodig als `AskHtml` public is; voeg wél `public partial class Program { }` toe onderaan `Program.cs` (nodig voor `WebApplicationFactory<Program>`).

- [x] **Step 3: Falende tests `tests/Api.Tests/AskHtmlTests.cs`**
```csharp
using SocialeKaartRag.Api;
using SocialeKaartRag.Core;
using SocialeKaartRag.Core.Generation;
using SocialeKaartRag.Core.Trace;

namespace SocialeKaartRag.Api.Tests;

public class AskHtmlTests
{
    private static AskResult Answered() => new("abc123", TraceOutcome.Answered, null,
        new Answer([new("Feit <b>één</b>", "fact", ["c1"]), new("Samenvatting", "summary", ["c1", "c2"])], "high", "Wilt u meer weten?"),
        [new SourceRef("c1", "osm:node/1", "https://www.openstreetmap.org/node/1", "Wijkcentrum X", null, "© OpenStreetMap-bijdragers (ODbL 1.0)"),
         new SourceRef("c2", "sc:9", "https://www.zoetermeer.nl/x", "Regeling Y", "2026-07-01", "Bron: gemeente Zoetermeer via Samenwerkende Catalogi (overheid.nl, CC0)")],
        "1.0.1");

    [Fact]
    public void Renders_items_with_kind_badges_citation_numbers_and_escapes_html()
    {
        var html = AskHtml.Render(Answered());
        Assert.Contains("badge-feit", html);
        Assert.Contains("badge-samenvatting", html);
        Assert.Contains("Feit &lt;b&gt;één&lt;/b&gt;", html);
        Assert.DoesNotContain("<b>één</b>", html);
        Assert.Contains("<sup", html);
        Assert.Contains(">[1]<", html);
        Assert.Contains(">[2]<", html);
    }

    [Fact]
    public void Renders_sources_with_links_attribution_and_correlation_id()
    {
        var html = AskHtml.Render(Answered());
        Assert.Contains("href=\"https://www.openstreetmap.org/node/1\"", html);
        Assert.Contains("© OpenStreetMap-bijdragers (ODbL 1.0)", html);
        Assert.Contains("laatst geverifieerd 2026-07-01", html);
        Assert.Contains("abc123", html);
        Assert.Contains("href=\"/trace/abc123\"", html);
        Assert.Contains("Wilt u meer weten?", html);
        Assert.Contains("policy 1.0.1", html);
    }

    [Theory]
    [InlineData(TraceOutcome.RefusedMedical, "huisarts")]
    [InlineData(TraceOutcome.RefusedScope, "buiten de sociale kaart")]
    [InlineData(TraceOutcome.Escalated, "geen betrouwbare bron")]
    public void Renders_refusal_and_escalation_messages(TraceOutcome outcome, string expected)
    {
        var msg = outcome switch { TraceOutcome.RefusedMedical => Core.Policy.RefusalTexts.Medical, TraceOutcome.RefusedScope => Core.Policy.RefusalTexts.OutOfScope, _ => Core.Policy.RefusalTexts.Escalated };
        var html = AskHtml.Render(new AskResult("id0", outcome, msg, null, [], "1.0.1"));
        Assert.Contains(expected, html);
        Assert.Contains("outcome-" + outcome.ToString().ToLowerInvariant(), html);
        Assert.DoesNotContain("badge-feit", html);
    }

    [Fact]
    public void Unsafe_source_url_is_not_linked()
    {
        var r = new AskResult("id0", TraceOutcome.Answered, null, new Answer([new("t", "fact", ["c1"])], "high", null),
            [new SourceRef("c1", "s", "javascript:alert(1)", "H", null, null)], "1.0.1");
        var html = AskHtml.Render(r);
        Assert.DoesNotContain("href=\"javascript:", html);
    }
}
```

- [x] **Step 4: `src/Api/AskHtml.cs`**
```csharp
using System.Text;
using System.Text.Encodings.Web;
using SocialeKaartRag.Core;
using SocialeKaartRag.Core.Trace;

namespace SocialeKaartRag.Api;

/// <summary>Server-gerenderd fragment voor de htmx-pagina. Alles wat van model of bron komt wordt HTML-encoded.</summary>
public static class AskHtml
{
    private static readonly HtmlEncoder H = HtmlEncoder.Default;

    public static string Render(AskResult r)
    {
        var sb = new StringBuilder();
        var outcome = r.Outcome.ToString().ToLowerInvariant();
        sb.Append("<article class=\"result outcome-").Append(outcome).AppendLine("\">");

        if (r.Outcome != TraceOutcome.Answered || r.Answer is null)
        {
            sb.Append("<p class=\"message\">").Append(H.Encode(r.Message ?? "")).AppendLine("</p>");
        }
        else
        {
            var order = r.Sources.Select((s, i) => (s.Id, N: i + 1)).ToDictionary(x => x.Id, x => x.N);
            sb.AppendLine("<ol class=\"answers\">");
            foreach (var item in r.Answer.Items)
            {
                var badge = item.Kind == "fact" ? "feit" : "samenvatting";
                sb.Append("<li><span class=\"badge badge-").Append(badge).Append("\">").Append(badge).Append("</span> ")
                  .Append(H.Encode(item.Text));
                foreach (var c in item.Citations)
                    if (order.TryGetValue(c, out var n)) sb.Append("<sup><a href=\"#src-").Append(n).Append("\">[").Append(n).Append("]</a></sup>");
                sb.AppendLine("</li>");
            }
            sb.AppendLine("</ol>");
            if (!string.IsNullOrWhiteSpace(r.Answer.FollowUp))
                sb.Append("<p class=\"follow-up\">").Append(H.Encode(r.Answer.FollowUp)).AppendLine("</p>");
            sb.AppendLine("<h3>Bronnen</h3><ol class=\"sources\">");
            foreach (var (s, n) in r.Sources.Select((s, i) => (s, i + 1)))
            {
                sb.Append("<li id=\"src-").Append(n).Append("\">");
                var title = H.Encode(s.Heading ?? s.SourceId);
                if (IsSafeHttpUrl(s.Url)) sb.Append("<a href=\"").Append(H.Encode(s.Url!)).Append("\" rel=\"noopener noreferrer\" target=\"_blank\">").Append(title).Append("</a>");
                else sb.Append(title);
                if (s.LastVerified is not null) sb.Append(" <small>(laatst geverifieerd ").Append(H.Encode(s.LastVerified)).Append(")</small>");
                if (s.Attribution is not null) sb.Append(" <small class=\"attribution\">").Append(H.Encode(s.Attribution)).Append("</small>");
                sb.AppendLine("</li>");
            }
            sb.AppendLine("</ol>");
        }

        sb.Append("<footer class=\"meta\">correlation-id <a href=\"/trace/").Append(H.Encode(r.CorrelationId)).Append("\"><code>")
          .Append(H.Encode(r.CorrelationId)).Append("</code></a> · policy ").Append(H.Encode(r.PolicyVersion)).AppendLine("</footer>");
        sb.AppendLine("</article>");
        return sb.ToString();
    }

    private static bool IsSafeHttpUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp);
}
```

- [x] **Step 5: Endpoint + pagina**

`AskEndpoint.cs` — voeg toe naast `/ask`:
```csharp
        app.MapPost("/ask/fragment", async (HttpRequest request, IAskOrchestrator orchestrator, HttpContext http, CancellationToken ct) =>
        {
            var form = await request.ReadFormAsync(ct);
            var question = form["question"].ToString();
            if (string.IsNullOrWhiteSpace(question) || question.Length > MaxQuestionLength)
                return Results.Content("<p class=\"message\">Stel een vraag van maximaal 1000 tekens.</p>", "text/html; charset=utf-8", statusCode: 400);
            var correlationId = Guid.NewGuid().ToString("n");
            http.Response.Headers["X-Correlation-Id"] = correlationId;
            var result = await orchestrator.AskAsync(question, correlationId, ct);
            return Results.Content(AskHtml.Render(result), "text/html; charset=utf-8");
        }).DisableAntiforgery();
```
(`DisableAntiforgery()` is nodig voor form-posts zonder antiforgery-token in minimal APIs; er zijn geen cookies/sessies, dus CSRF is niet van toepassing.)

`Program.cs`: `app.UseDefaultFiles(); app.UseStaticFiles();` vóór `app.MapAsk();`.

`src/Api/wwwroot/index.html`:
```html
<!doctype html>
<html lang="nl">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Sociale kaart — gids (demo)</title>
  <link rel="stylesheet" href="/site.css">
  <script src="/htmx.min.js" defer></script>
</head>
<body>
  <main>
    <h1>Sociale kaart — gids <small>demo · Den Haag &amp; Zoetermeer</small></h1>
    <p class="intro">Stel een vraag over hulp, regelingen of voorzieningen. De gids antwoordt alleen op basis van
      bronnen en geeft geen medisch advies. Vermeld geen BSN, adres of telefoonnummer — die worden sowieso weggehaald.</p>
    <form hx-post="/ask/fragment" hx-target="#result" hx-indicator="#busy">
      <label for="question">Uw vraag</label>
      <textarea id="question" name="question" maxlength="1000" rows="3" required
        placeholder="bv. waar is een wijkcentrum in de buurt van 2511CV?"></textarea>
      <button type="submit">Vraag</button>
      <span id="busy" class="htmx-indicator">even geduld…</span>
    </form>
    <section id="result" aria-live="polite"></section>
    <footer>
      <p>Bronnen: gemeenten via Samenwerkende Catalogi (CC0) · © OpenStreetMap-bijdragers (ODbL) · PDOK (CC0).
        Elk antwoord linkt naar zijn bron. Geen accounts, geen cookies. Broncode:
        <a href="https://github.com/jelleschut/sociale-kaart-rag">github.com/jelleschut/sociale-kaart-rag</a>.</p>
    </footer>
  </main>
</body>
</html>
```
`site.css`: compact, systeemfont, badges (`.badge-feit` groen, `.badge-samenvatting` blauw), `.outcome-refused_medical`/`.outcome-refused_scope`/`.outcome-escalated` met gele rand, `.htmx-indicator{display:none} .htmx-request .htmx-indicator{display:inline}`.

`htmx.min.js`: vendor de laatste 2.x: `curl -sL -o src/Api/wwwroot/htmx.min.js https://unpkg.com/htmx.org@2/dist/htmx.min.js` en noteer versie + SHA-256 in een commentaar bovenin `index.html`. Geen CDN-verwijzing (CSP `default-src 'self'` in Task 2).

- [x] **Step 6: Endpoint-tests `tests/Api.Tests/EndpointTests.cs`** (WebApplicationFactory, `IAskOrchestrator` vervangen door een fake; Azure-clients vervangen door… `AddAzureClients` bouwt clients zonder netwerk aan te roepen — alleen `AzureOptions` moet ingevuld zijn: geef `Azure__*` dummy-URL's via `builder.UseSetting`)
```csharp
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
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
    public async Task Json_ask_still_works()
    {
        var resp = await f.CreateClient().PostAsJsonAsync("/ask", new { question = "waar is hulp?" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Contains("\"outcome\":\"answered\"", await resp.Content.ReadAsStringAsync());
    }
}
```
(`RemoveAll` uit `Microsoft.Extensions.DependencyInjection.Extensions`.) `BlobTraceSink`/`AppInsights` worden nooit aangeroepen omdat de fake-orchestrator geen trace schrijft.

- [x] **Step 7: Run — groen; lokaal bekijken** (`dotnet run --project src/Api --urls http://localhost:5088` met de Azure-env-vars, open http://localhost:5088). Commit via PR — `feat(ui): htmx-pagina met feit/samenvatting-badges, citaties, attributie en correlation-id`

---

### Task 2: Hardening voor een publieke URL + follow-up #1 (modelVersion)

**Files:** `src/Api/Program.cs`, `src/Core/Trace/TraceRecord.cs`, `src/Core/AskOrchestrator.cs`, `tests/Api.Tests/HardeningTests.cs`, `tests/Core.Tests/TraceRecordTests.cs`

- [x] **Step 1: Falende tests**
```csharp
// tests/Api.Tests/HardeningTests.cs
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
    { public Task<AskResult> AskAsync(string q, string id, CancellationToken ct = default) => throw new InvalidOperationException("boom-detail"); }
}
```
Trace-test: `TraceRecord` krijgt `ModelVersion`; `AskOrchestrator` vult `Model = "gpt-4.1-mini"` en `ModelVersion = "2025-04-14"` uit een modelstring `gpt-4.1-mini-2025-04-14` (helper `SplitModel(string)` → (name, version?)); test in `TraceRecordTests`: `SplitModel("gpt-4.1-mini-2025-04-14") == ("gpt-4.1-mini","2025-04-14")`, `SplitModel("gpt-4.1-mini") == ("gpt-4.1-mini", null)`.

- [x] **Step 2: Implementatie**
- Headers: middleware `app.Use(async (ctx, next) => { ctx.Response.Headers["Content-Security-Policy"] = "default-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'"; …; await next(); });`
- Rate limiting: `builder.Services.AddRateLimiter(o => { o.RejectionStatusCode = 429; o.AddFixedWindowLimiter("ask", w => { w.PermitLimit = 20; w.Window = TimeSpan.FromMinutes(1); w.QueueLimit = 0; }); });` `app.UseRateLimiter();` en `.RequireRateLimiting("ask")` op `/ask` en `/ask/fragment`. Partitie per client-IP: gebruik `o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "anon", _ => new() { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));` — kies één van beide (GlobalLimiter per IP is het eenvoudigst; test verwacht 429 binnen 25 calls).
- Exception handler: `builder.Services.AddProblemDetails(); app.UseExceptionHandler();` (ProblemDetails zonder detail in Production; in tests draait de host in `Development` → zet expliciet `builder.Environment.EnvironmentName`-onafhankelijk gedrag via `app.UseExceptionHandler(e => e.Run(async ctx => { ctx.Response.StatusCode = 500; await ctx.Response.WriteAsJsonAsync(new { type = "about:blank", title = "Interne fout", status = 500, correlationId = ctx.Response.Headers["X-Correlation-Id"].ToString() }); }));`).
- `TraceRecord.ModelVersion` + `SplitModel` in `AskOrchestrator` (statisch, public voor de test).

- [x] **Step 3: Run — groen; commit via PR** — `feat(api): CSP/headers, per-IP rate limiting, ProblemDetails-exception-handler; modelVersion in trace`

---

### Task 3: ADR-0001, ADR-0004, traceability.md, README

**Files:** `docs/adr/0001-application-owned-orchestration.md`, `docs/adr/0004-guardrails-buiten-het-model.md`, `docs/traceability.md`, `README.md`, spec §10 stale regel

- [x] **Step 1: ADR-0001** — context (spec §3), opties: (A) application-owned orchestration in .NET met Foundry als model-runtime; (B) Azure AI Foundry Agent Service met AI Search-tool; (C) Semantic Kernel / Agent Framework. Afweging per criterium: controle over guardrails buiten het model (A ✔, B ✘: tool-keuze bij het model, C ±), evalueerbaarheid/traceability (A: elke stap eigen code en trace-veld), lock-in, kosten (A: alleen model-calls), complexiteit (B/C minder code, maar zwarte dozen). Besluit A; gevolgen: meer eigen code, maar elke policy-stap getest (verwijs naar `AskOrchestrator`, eval-categorieën).
- [x] **Step 2: ADR-0004** — de vijf lagen (PII-regex vóór het model; intent-classificatie met enum-schema; tool-allow-list met orchestrator-aanroep; untrusted-content-boundary met source-blokken + neutralisatie; escalatie op score/citaties + citatiefilter), waarom niet alleen system-prompt-instructies (bewezen door canaries: i01–i05 in de eval), residuele risico's (classifier-sturing door de vraag; zelfbeoordeling van de judge) en hoe de eval die meet.
- [x] **Step 3: `docs/traceability.md`** — tabel veld → waarom → waar gezet (uit `TraceRecord`: correlationId, timestamp, policyVersion, model, modelVersion, promptHash, piiRedacted/piiTypes, intent, domain, toolCalls[name, argumentsHash, resultCount], retrievedChunkIds/scores, tokensIn/Out/Cached, estimatedCostEur, latencyMs, outcome, refusalReason) + wat er bewust NIET in zit (vraag- en antwoordtekst) + waar traces landen (Blob `traces/yyyy/MM/dd.jsonl` + `by-id/`, 90 d; App Insights event `rag.request` + metrics) + hoe je van een correlation-id in de UI naar `/trace/{id}` en de Blob-regel komt.
- [x] **Step 4: README herschrijven** — secties: Doel (spec §1) · Live demo (URL, wat je kunt proberen: 3 voorbeeldvragen incl. een medische) · Architectuur (mermaid C2: browser → Container App API [PII → intent → search-tool → generatie → citatiefilter → trace] → AI Search / OpenAI / Blob / App Insights; ingest-console → SC/OSM/PDOK → Search) · Guardrails (5 lagen, link ADR-0004) · Evaluatie (bestaande sectie) · Bronnen en attributie (bestaand) · Traceability (link) · Kosten (raming spec §7 + gemeten: eval € 0,03/run, ~€ 0,001/vraag; `az` month-to-date invullen) · Wat dit wel/niet bewijst (wel: RAG-architectuur, guardrails buiten het model, traceability, IaC, CI-scans, eval; niet: schaal, SLA, echte gebruikersdata, denhaag.nl-tekst) · Ontwikkelen (bootstrap, terraform, ingest, eval, tests) · ADR-index · Plannen-index. Verwijder de gestapelde status-regels; één "Status"-regel met datum.
- [x] **Step 5: Stale verwijzingen** — spec §10 gate 3 "Ingest kb-chunks.jsonl" → voetnoot "(historisch; kb-corpus verwijderd 28-08)"; `followups-na-plan-1.md` items 1, 5, 13, 16 afvinken.
- [x] **Step 6: Commit via PR** — `docs: ADR-0001, ADR-0004, traceability.md en README met architectuurplaat`

---

### Task 4: Deploy, gate 7 en kostencheck

- [ ] **Step 1: `deploy.yml` smoke uitbreiden**: na `/healthz` ook `curl -fsS "$API_URL/" | grep -q "Sociale kaart"`.
- [ ] **Step 2: Deploy** — merge triggert `deploy`; Jelle keurt goed in environment `azure`. Daarna live: `GET /` toont de pagina; via de UI: "waar is een wijkcentrum in de buurt van 2511CV?" → gecieerd antwoord met badges/attributie/correlation-id; "ik heb koorts, wat heb ik?" → weigering. Koude start ≤ 20 s.
- [ ] **Step 3: Kosten** — `az rest` CostManagement query month-to-date voor `rg-skr-9asax` → bedrag in README "Kosten".
- [ ] **Step 4: Plan afvinken, README-status, follow-ups; commit via PR** — `docs: live-URL, kosten en status na plan 4`

---

## Self-review

**Spec §4.7:** `POST /ask`, `GET /healthz`, `GET /trace/{id}` bestonden; de pagina (vraag, badges feit/samenvatting, citaties met bronlink, zichtbare correlation-id, weigerings-/escalatieteksten) → Task 1; geen sessies/accounts/cookies ✔. **§9:** README (doel, architectuurplaat C2, wel/niet-bewijs, kosten) → Task 3; ADR-0001, 0002 ✔(plan 2), 0003 ✔(plan 1), 0004 → Task 3; traceability.md → Task 3; eval-report ✔(plan 3). **§10 gate 7** → Task 4. **Follow-ups meegenomen:** #1 modelVersion (Task 2), #5 exception-handler (Task 2), #13 stale refs (Task 3), #16 attributie in UI (Task 1).

**Type-consistentie:** `AskResult`/`SourceRef` zoals in Core; `IAskOrchestrator` nieuw en door `AskOrchestrator` geïmplementeerd; `RefusalTexts` in `SocialeKaartRag.Core.Policy`; `TraceOutcome` in `SocialeKaartRag.Core.Trace`.

**Onzekerheden:** `WebApplicationFactory<Program>` vereist `public partial class Program {}`; `AddAzureClients` maakt `DefaultAzureCredential` aan zonder netwerk (ok) — als een client-constructor toch valideert, registreer in de test-factory dummy-instanties; rate-limit-test in-memory per IP (`RemoteIpAddress` is null in TestServer → partitie "anon", werkt); htmx 2.x exacte versie bij vendoren noteren.
