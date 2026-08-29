namespace SocialeKaartRag.Ingest.Sources;

/// <summary>Bron-neutraal tussenmodel; SocialMapChunker maakt hier een Chunk van.</summary>
public sealed record SocialMapRecord
{
    public required string Source { get; init; }        // "sc" | "osm"
    public required string SourceId { get; init; }      // "sc:<productID>" | "osm:node/123"
    public required string SourceUrl { get; init; }
    public required string Name { get; init; }
    /// <summary>Primaire categorie (eerste uit <see cref="Categories"/>); null = buiten de taxonomie.</summary>
    public string? Category { get; init; }
    /// <summary>Alle categorieën, primaire eerst (ADR-0006).</summary>
    public string[] Categories { get; init; } = [];
    public string? Summary { get; init; }
    public string? BodyText { get; init; }              // gemeentepagina (sc) of description (osm)
    public required string Municipality { get; init; }  // "Den Haag" | "Zoetermeer"
    public string? Street { get; init; }                // zonder huisnummer
    public string? Postcode { get; init; }
    public string? Phone { get; init; }
    public string? Website { get; init; }
    public string? OpeningHours { get; init; }
    public double? Lat { get; init; }
    public double? Lon { get; init; }
    public string? LastModified { get; init; }
    public required string Attribution { get; init; }
}
