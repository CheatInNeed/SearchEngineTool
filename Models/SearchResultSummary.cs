using System.IO;

namespace FileSearchTool.Models;

public record SearchResultSummary(
    string FilePath,
    float FinalScore,
    float LuceneScore,
    float ProximityScore,
    float PhraseBonus,
    float DensityScore,
    float AllTermsBonus,
    int SpanLength,
    int MatchedDistinctTerms,
    bool ContainsAllTerms,
    float FreshnessScore,
    float FiletypeScore,
    float FolderDepthScore,
    float EarlyScore,
    float CoverageScore)
{
    public string FileName => Path.GetFileName(FilePath);

    public string HitSummary =>
        $"Final: {FinalScore:0.00}  ·  Lucene: {LuceneScore:0.00}  ·  prox: {ProximityScore:0.00}  ·  phrase: {PhraseBonus:0.00}  ·  dens: {DensityScore:0.00}  ·  all: {AllTermsBonus:0.00}  ·  fresh: {FreshnessScore:0.00}  ·  type: {FiletypeScore:0.00}  ·  depth: {FolderDepthScore:0.00}  ·  early: {EarlyScore:0.00}  ·  cov: {CoverageScore:0.00}  ·  span: {SpanLength}";
}
