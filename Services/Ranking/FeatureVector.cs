namespace FileSearchTool.Services.Ranking;

public sealed class FeatureVector
{
    public float LuceneScore { get; init; }
    public float ProximityScore { get; init; }
    public float PhraseBonus { get; init; }
    public float DensityScore { get; init; }
    public float AllTermsBonus { get; init; }

    public float FreshnessScore { get; init; }
    public float FiletypeScore { get; init; }
    public float FolderDepthScore { get; init; }

    public float EarlyScore { get; init; }
    public float CoverageScore { get; init; }
}