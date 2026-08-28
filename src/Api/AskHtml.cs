using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using SocialeKaartRag.Core;
using SocialeKaartRag.Core.Trace;

namespace SocialeKaartRag.Api;

/// <summary>Server-gerenderd fragment voor de htmx-pagina. Alles wat van model of bron komt wordt HTML-encoded.</summary>
public static class AskHtml
{
    // UnicodeRanges.All: pagina is utf-8, dus geen numerieke escapes nodig voor bv. "één" — alleen HTML-syntax wordt geëscaped.
    private static readonly HtmlEncoder H = HtmlEncoder.Create(UnicodeRanges.All);

    public static string Render(AskResult r)
    {
        var sb = new StringBuilder();
        var outcome = OutcomeClass(r.Outcome);
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

    /// <summary>Zelfde snake_case als de JSON-API; de CSS-classes in site.css volgen deze namen.</summary>
    public static string OutcomeClass(TraceOutcome o) => o switch
    {
        TraceOutcome.Answered => "answered", TraceOutcome.RefusedMedical => "refused_medical", TraceOutcome.RefusedScope => "refused_scope",
        TraceOutcome.Escalated => "escalated", _ => "error",
    };
}
