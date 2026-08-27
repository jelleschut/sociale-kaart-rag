using SocialeKaartRag.Core.Generation;

namespace SocialeKaartRag.Core.Policy;

/// <summary>Elk antwoord-item zonder (bestaand) citaat wordt verwijderd (spec §4.4). Lege uitkomst → escalatie in de orchestrator.</summary>
public static class CitationFilter
{
    public static Answer Apply(Answer answer, IReadOnlySet<string> retrievedChunkIds)
    {
        var items = answer.Items
            .Select(i => i with { Citations = i.Citations.Where(retrievedChunkIds.Contains).ToArray() })
            .Where(i => i.Citations.Length > 0)
            .ToArray();
        return answer with { Items = items };
    }
}
