namespace FileSearchTool.Services.Ranking;

public static class RankingWeights
{
    // Proximity: α / (1 + spanLength)
    public const float ProximityAlpha = 4.0f;

    // Density: (matchedTermsCount / spanLength) * β
    public const float DensityBeta = 2.0f;

    // Phrase bonuses
    public const float ExactPhraseBonus = 6.0f;
    public const float NearPhraseBonus = 3.0f;

    // Near phrase definition (span in words between first and last token)
    public const int NearPhraseMaxSpan = 5; // “inden for 3–5 ord”

    // All-terms bonus
    public const float AllTermsBonus = 2.5f;

    // Safety / edge cases
    public const int MinSpanFallback = 50; // if something goes weird, prevents division explosions
}