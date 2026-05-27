using System.Collections.Generic;
using System.IO;

namespace FileSearchTool.Models;

public record SearchResultSummary(
    string FilePath,
    int? PageNumber,
    IReadOnlyList<SnippetPart> SnippetParts,
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
    float CoverageScore,
    float SubPhraseBonus)
{
    public string FileName => Path.GetFileName(FilePath);
    public string FolderName => Path.GetFileName(Path.GetDirectoryName(FilePath) ?? "");
    public string Extension => Path.GetExtension(FilePath).ToLowerInvariant();
    public string PageLabel => PageNumber.HasValue ? $"Page {PageNumber.Value}" : "";
    public bool HasPageNumber => PageNumber.HasValue;

    public string ScoreLine =>
        $"Score {FinalScore:0.00} · Lucene {LuceneScore:0.00} · proximity {ProximityScore:0.00} · phrase {PhraseBonus + SubPhraseBonus:0.00}";

    public string SignalLine =>
        ContainsAllTerms
            ? $"All terms · coverage {CoverageScore:0.00} · span {SpanLength} · {Extension}"
            : $"Partial match · coverage {CoverageScore:0.00} · span {SpanLength} · {Extension}";

    public string HitSummary =>
        $"Final: {FinalScore:0.00}  ·  Lucene: {LuceneScore:0.00}  ·  prox: {ProximityScore:0.00}  ·  phrase: {PhraseBonus:0.00}  ·  dens: {DensityScore:0.00}  ·  all: {AllTermsBonus:0.00}  ·  fresh: {FreshnessScore:0.00}  ·  type: {FiletypeScore:0.00}  ·  depth: {FolderDepthScore:0.00}  ·  early: {EarlyScore:0.00}  ·  cov: {CoverageScore:0.00}  ·  span: {SpanLength} ·  sub: {SubPhraseBonus}";
}
