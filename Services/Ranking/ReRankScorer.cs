using System;
using System.Collections.Generic;
using System.Linq;

namespace FileSearchTool.Services.Ranking;

public sealed class ReRankBreakdown
{
    public float LuceneScore { get; }
    public float ProximityScore { get; }
    public float PhraseBonus { get; }
    public float SubPhraseBonus { get; }
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
        float subPhraseBonus,
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
        SubPhraseBonus = subPhraseBonus;
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
        IReadOnlyList<string> analyzedTokens,
        float luceneScore,
        string? storedContent,
        string ext,
        long lastModifiedTicks,
        int folderDepth,
        int corpusN,
        Dictionary<string,int> dfByTerm,
        LinearModel? model)
    {
        var qTokens = analyzedTokens
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // If no query tokens or no text: just keep Lucene score
        if (qTokens.Count == 0 || string.IsNullOrWhiteSpace(storedContent))
        {
            return new ReRankBreakdown(
                luceneScore,
                0f,
                0f,
                0f,
                0f,
                0f,
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
                0f,
                0f,
                0f,
                0f,
                0f,
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
                0f,          // subPhraseBonus (ingen for single-token mode)
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

        float earlyScore = 0f;

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
        
        // Bonus: hvis vi har en meget tæt gruppe (>=2 forskellige tokens tæt)
        // selv hvis ikke alle query-termer er i samme vindue.
        float closeGroupBonus = 0f;
        if (window != null)
        {
            // window.SpanLength er afstand mellem første og sidste token i bedste vindue
            // window.MatchedDistinctTerms er antal forskellige query-termer i vinduet

            if (window.MatchedDistinctTerms >= 2 && window.SpanLength <= 3)
                closeGroupBonus = 3.5f;  // stærkt signal ("et par ord")

            else if (window.MatchedDistinctTerms >= 2 && window.SpanLength <= 6)
                closeGroupBonus = 1.8f;  // lidt bredere, stadig signal
        }

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
        // Sub-phrase bonus: boost query bigrams/trigrams (phrase inside phrase)
        // Example: "process control block pcb" should still reward "process control block"
        float subPhraseBonus = ComputeSubPhraseBonus(normText, qTokens);

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
        float headingScore = HeadingFeatures.HeadingBoost(storedContent!, qTokens);
        
        var fv = new FeatureVector
        {
            LuceneScore = luceneScore,
            ProximityScore = proximityScore,
            PhraseBonus = phraseBonus,
            SubPhraseBonus = subPhraseBonus,
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
            : (luceneScore + proximityScore + phraseBonus + subPhraseBonus + densityScore + allTermsBonus
               + freshnessScore + filetypeScore + folderDepthScore
               + coverageScore
               + closeGroupBonus
               + headingScore);


        return new ReRankBreakdown(
            luceneScore,
            proximityScore,
            phraseBonus,
            subPhraseBonus,
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
    
    private static float ComputeSubPhraseBonus(string normText, List<string> qTokens)
{
    // Slop-baseret subphrase bonus:
    // belønner bigrams/trigrams hvor ordene står tæt (ikke nødvendigvis direkte ved siden af)
    if (qTokens.Count < 3) return 0f;

    const int maxGapWords = 2;      // "et par ord imellem"
    const float trigramBoost = 2.8f;
    const float bigramBoost  = 1.4f;

    const float maxBonus = 6.0f;

    // let query-length normalisering (som I havde før-ish)
    float qNorm = 1f / MathF.Max(1f, MathF.Log(1f + qTokens.Count));

    // normText er allerede normaliseret og whitespace-splittet i ScoreDocument,
    // men her får vi kun normText-string. Split den igen (ok performance for top-200).
    var words = normText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (words.Length == 0) return 0f;

    // Byg positions map: token -> positions
    var pos = new Dictionary<string, List<int>>(StringComparer.Ordinal);
    for (int i = 0; i < words.Length; i++)
    {
        var w = words[i];
        if (!pos.TryGetValue(w, out var list))
        {
            list = new List<int>();
            pos[w] = list;
        }
        list.Add(i);
    }

    bool NearBigram(string a, string b)
    {
        if (!pos.TryGetValue(a, out var pa) || !pos.TryGetValue(b, out var pb)) return false;

        // two-pointer scan (pa og pb er sorterede)
        int i = 0, j = 0;
        while (i < pa.Count && j < pb.Count)
        {
            int A = pa[i];
            int B = pb[j];

            if (B > A)
            {
                int gap = B - A - 1;           // antal ord imellem
                if (gap <= maxGapWords) return true;
                i++;                            // prøv næste A (mere til højre)
            }
            else
            {
                j++;                            // B skal være efter A
            }
        }
        return false;
    }

    bool NearTrigram(string a, string b, string c)
    {
        // Krav: a~b indenfor gap OG b~c indenfor gap (svagere, men god signal)
        // Alternativt kunne man kræve a..c indenfor en samlet span, men dette fungerer fint.
        return NearBigram(a, b) && NearBigram(b, c);
    }

    var matched = new HashSet<string>(StringComparer.Ordinal);
    float bonus = 0f;

    // Trigrams (unikke)
    for (int i = 0; i <= qTokens.Count - 3; i++)
    {
        var a = qTokens[i];
        var b = qTokens[i + 1];
        var c = qTokens[i + 2];
        var key = $"{a} {b} {c}";

        if (matched.Add(key) && NearTrigram(a, b, c))
            bonus += trigramBoost;
    }

    // Bigrams (unikke)
    for (int i = 0; i <= qTokens.Count - 2; i++)
    {
        var a = qTokens[i];
        var b = qTokens[i + 1];
        var key = $"{a} {b}";

        if (matched.Add(key) && NearBigram(a, b))
            bonus += bigramBoost;
    }

    bonus *= qNorm;
    return MathF.Min(bonus, maxBonus);
}

}
