namespace SocialeKaartRag.Core.Policy;

/// <summary>Semver van system-prompt + drempels; staat in elke trace (spec §4.3). Verhogen bij elke prompt-/drempelwijziging.</summary>
public static class PolicyVersion
{
    public const string Current = "1.0.0";
    /// <summary>Hybrid-search (RRF) scores liggen typisch 0,01–0,04; gemeten 27-08: relevante hits 0,030–0,032.</summary>
    public const double EscalationScoreThreshold = 0.015;
}
