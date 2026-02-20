using System;
using System.Collections.Generic;
using System.Linq;

namespace FileSearchTool.Services.Ranking;

public sealed class WindowResult
{
    public int SpanLength { get; }
    public int MatchedDistinctTerms { get; }
    public bool ContainsAllTerms { get; }

    public WindowResult(int spanLength, int matchedDistinctTerms, bool containsAllTerms)
    {
        SpanLength = spanLength;
        MatchedDistinctTerms = matchedDistinctTerms;
        ContainsAllTerms = containsAllTerms;
    }
}

public static class ProximityWindowFinder
{
    // positionsByToken: token -> sorted positions (word indices)
    public static WindowResult? FindBestWindow(
        IReadOnlyList<string> queryTokens,
        Dictionary<string, List<int>> positionsByToken)
    {
        if (queryTokens.Count < 2) return null;

        var distinctTokens = queryTokens
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (distinctTokens.Count < 2) return null;

        // Build merged stream: (pos, token)
        var merged = new List<(int pos, string token)>();
        foreach (var token in distinctTokens)
        {
            if (!positionsByToken.TryGetValue(token, out var list)) continue;
            foreach (var p in list)
                merged.Add((p, token));
        }

        if (merged.Count < 2) return null;

        merged.Sort((a, b) => a.pos.CompareTo(b.pos));

        // Try: window containing ALL tokens
        var all = SlidingWindow(merged, distinctTokens.Count);
        if (all != null)
            return new WindowResult(all.Value.span, all.Value.distinct, containsAllTerms: true);

        // Fallback: window containing >=2 distinct tokens
        var two = SlidingWindow(merged, 2);
        if (two != null)
            return new WindowResult(two.Value.span, two.Value.distinct, containsAllTerms: false);

        return null;
    }

    private static (int span, int distinct)? SlidingWindow(List<(int pos, string token)> merged, int requiredDistinct)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        int haveDistinct = 0;

        int left = 0;
        int bestSpan = int.MaxValue;
        int bestDistinct = 0;

        for (int right = 0; right < merged.Count; right++)
        {
            var t = merged[right].token;
            if (!counts.TryGetValue(t, out var c))
                counts[t] = 0;

            counts[t] = c + 1;
            if (c == 0) haveDistinct++;

            while (haveDistinct >= requiredDistinct && left <= right)
            {
                int span = merged[right].pos - merged[left].pos;
                if (span < bestSpan)
                {
                    bestSpan = span;
                    bestDistinct = haveDistinct;
                }

                var lt = merged[left].token;
                counts[lt]--;
                if (counts[lt] == 0) haveDistinct--;
                left++;
            }
        }

        return bestSpan == int.MaxValue ? null : (bestSpan, bestDistinct);
    }
}
