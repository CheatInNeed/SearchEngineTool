using System;
using System.Collections.Generic;
using System.Linq;

namespace FileSearchTool.Services.Ranking;

public sealed class ReRankBreakdown
{
    public float LuceneScore { get; }
    public float ProximityScore { get; }
    public float PhraseBonus { get; }
    public float DensityScore { get; }
    public float AllTermsBonus { get; }
    public float FinalScore { get; }
    public int SpanLength { get; }
    public int MatchedDistinctTerms { get; }
    public bool ContainsAllTerms { get; }
    public float FreshnessScore { get; }
    public float FiletypeScore { get; }
    public float FolderDepthScore { get; }
    public float EarlyScore { get; }
    public float CoverageScore { get; }

    public ReRankBreakdown(
        float luceneScore,
        float proximityScore,
        float phraseBonus,
        float densityScore,
        float allTermsBonus,
        float finalScore,
        int spanLength,
        int matchedDistinctTerms,
        bool containsAllTerms,
        float freshnessScore,
        float filetypeScore,
        float folderDepthScore,
        float earlyScore,
        float coverageScore)
    {
        LuceneScore = luceneScore;
        ProximityScore = proximityScore;
        PhraseBonus = phraseBonus;
        DensityScore = densityScore;
        AllTermsBonus = allTermsBonus;
        FinalScore = finalScore;
        SpanLength = spanLength;
        MatchedDistinctTerms = matchedDistinctTerms;
        ContainsAllTerms = containsAllTerms;

        FreshnessScore = freshnessScore;
        FiletypeScore = filetypeScore;
        FolderDepthScore = folderDepthScore;
        EarlyScore = earlyScore;
        CoverageScore = coverageScore;
    }
}

public static class ReRankScorer
{
    public static ReRankBreakdown ScoreDocument(
        string rawQuery,
        float luceneScore,
        string? storedContent,
        string ext,
        long lastModifiedTicks,
        int folderDepth,
        int corpusN,
        System.Collections.Generic.Dictionary<string,int> dfByTerm,
        LinearModel? model)
    {
        var qTokens = QueryTextUtil.Tokenize(rawQuery)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // If no query tokens or no text: just keep Lucene score
        if (qTokens.Count == 0 || string.IsNullOrWhiteSpace(storedContent))
        {
            return new ReRankBreakdown(
                luceneScore,
                0f, 0f, 0f, 0f,
                luceneScore,
                RankingWeights.MinSpanFallback,
                0,
                false,
                0f, 0f, 0f, 0f, 0f);
        }

        var normText = QueryTextUtil.Normalize(storedContent);
        var words = normText.Length == 0
            ? Array.Empty<string>()
            : normText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // If document ends up empty: keep Lucene score
        if (words.Length == 0)
        {
            return new ReRankBreakdown(
                luceneScore,
                0f, 0f, 0f, 0f,
                luceneScore,
                RankingWeights.MinSpanFallback,
                0,
                false,
                0f, 0f, 0f, 0f, 0f);
        }

        // Query < 2 tokens => proximity model not applied (but we still allow exact phrase + all-terms bonus)
        if (qTokens.Count < 2)
        {
            float allTermsBonus0 = ContainsAllTerms(qTokens, normText) ? RankingWeights.AllTermsBonus : 0f;
            float phraseBonus0 = ExactPhrase(normText, qTokens) ? RankingWeights.ExactPhraseBonus : 0f;

            float final0 = luceneScore + phraseBonus0 + allTermsBonus0;

            return new ReRankBreakdown(
                luceneScore,
                0f,
                phraseBonus0,
                0f,
                allTermsBonus0,
                final0,
                RankingWeights.MinSpanFallback,
                0,
                allTermsBonus0 > 0f,
                0f, 0f, 0f, 0f, 0f);
        }

        // Build token -> positions map
        var positions = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (int i = 0; i < words.Length; i++)
        {
            var w = words[i];
            if (!positions.TryGetValue(w, out var list))
            {
                list = new List<int>();
                positions[w] = list;
            }
            list.Add(i);
        }

        // Keep only query tokens
        var positionsByToken = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        // FYLD positionsByToken først
        foreach (var t in qTokens)
        {
            if (positions.TryGetValue(t, out var list) && list.Count > 0)
                positionsByToken[t] = list;
        }

        // Early-position boost: første forekomst af et query-term
        int firstPos = int.MaxValue;
        foreach (var kv in positionsByToken)
        {
            if (kv.Value.Count > 0)
                firstPos = Math.Min(firstPos, kv.Value[0]);
        }

        const float gamma = 2.0f; // tunable
        float earlyScore = (firstPos == int.MaxValue) ? 0f : (gamma / (1f + firstPos));

        // CoverageScore = sum(IDF(term) for matched terms)
        float coverageScore = 0f;
        foreach (var t in qTokens)
        {
            if (!positionsByToken.ContainsKey(t)) continue;
            dfByTerm.TryGetValue(t, out var df);
            coverageScore += IdfApprox.Compute(corpusN, df);
        }

        var window = ProximityWindowFinder.FindBestWindow(qTokens, positionsByToken);

        int spanLength = window != null ? window.SpanLength : RankingWeights.MinSpanFallback;
        if (spanLength < 0) spanLength = RankingWeights.MinSpanFallback;

        bool containsAll = window != null && window.ContainsAllTerms;
        int matchedDistinct = window != null ? window.MatchedDistinctTerms : 0;

        // Proximity score: α / (1 + spanLength)
        float proximityScore = (spanLength >= RankingWeights.MinSpanFallback)
            ? 0f
            : (RankingWeights.ProximityAlpha / (1f + spanLength));

        // Phrase bonus
        float phraseBonus = 0f;
        if (ExactPhrase(normText, qTokens))
        {
            phraseBonus += RankingWeights.ExactPhraseBonus;
        }
        else if (spanLength <= RankingWeights.NearPhraseMaxSpan && containsAll)
        {
            phraseBonus += RankingWeights.NearPhraseBonus;
        }

        // Density score: (matchedDistinctTerms / spanLength) * β
        float densityScore = 0f;
        if (spanLength > 0 &&
            spanLength < RankingWeights.MinSpanFallback &&
            matchedDistinct >= 2)
        {
            densityScore = (matchedDistinct / (float)spanLength) * RankingWeights.DensityBeta;
        }

        // All-terms bonus (global)
        float allTermsBonus = ContainsAllTerms(qTokens, normText) ? RankingWeights.AllTermsBonus : 0f;

        var nowTicks = DateTime.UtcNow.Ticks;
        float freshnessScore = MetadataFeatures.FreshnessBoost(nowTicks, lastModifiedTicks);
        float filetypeScore = MetadataFeatures.ExtensionPrior(ext);
        float folderDepthScore = MetadataFeatures.FolderDepthPrior(folderDepth);
        
        var fv = new FeatureVector
        {
            LuceneScore = luceneScore,
            ProximityScore = proximityScore,
            PhraseBonus = phraseBonus,
            DensityScore = densityScore,
            AllTermsBonus = allTermsBonus,

            FreshnessScore = freshnessScore,
            FiletypeScore = filetypeScore,
            FolderDepthScore = folderDepthScore,

            EarlyScore = earlyScore,
            CoverageScore = coverageScore
        };

        float finalScore = model != null
            ? model.Score(fv)
            : (luceneScore + proximityScore + phraseBonus + densityScore + allTermsBonus
               + freshnessScore + filetypeScore + folderDepthScore
               + earlyScore + coverageScore);


        return new ReRankBreakdown(
            luceneScore,
            proximityScore,
            phraseBonus,
            densityScore,
            allTermsBonus,
            finalScore,
            spanLength,
            matchedDistinct,
            containsAll,
            freshnessScore,
            filetypeScore,
            folderDepthScore,
            earlyScore,
            coverageScore);
    }

    private static bool ContainsAllTerms(List<string> qTokens, string normText)
    {
        foreach (var t in qTokens)
        {
            if (!normText.Contains(t, StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    private static bool ExactPhrase(string normText, List<string> qTokens)
    {
        if (qTokens.Count < 2) return false;
        var phrase = string.Join(' ', qTokens);
        return normText.Contains(phrase, StringComparison.Ordinal);
    }
}
