using System;
using System.Collections.Generic;

namespace FileSearchTool.Services.Benchmarking;

public static class Metrics
{
    public static double Mrr(IReadOnlyList<string> rankedPaths, IReadOnlyDictionary<string, int> relByPath)
    {
        for (int i = 0; i < rankedPaths.Count; i++)
        {
            if (relByPath.TryGetValue(rankedPaths[i], out var g) && g > 0)
                return 1.0 / (i + 1);
        }
        return 0.0;
    }

    public static double NdcgAtK(IReadOnlyList<string> rankedPaths, IReadOnlyDictionary<string, int> relByPath, int k)
    {
        if (k <= 0) return 0.0;

        var dcg = DcgAtK(rankedPaths, relByPath, k);
        var idcg = IdealDcgAtK(relByPath, k);

        if (idcg <= 0.0) return 0.0;
        return dcg / idcg;
    }

    private static double DcgAtK(IReadOnlyList<string> rankedPaths, IReadOnlyDictionary<string, int> relByPath, int k)
    {
        double sum = 0.0;
        var n = Math.Min(k, rankedPaths.Count);

        for (int i = 0; i < n; i++)
        {
            relByPath.TryGetValue(rankedPaths[i], out var rel);
            // standard graded DCG: (2^rel - 1) / log2(i+2)
            var gain = Math.Pow(2.0, rel) - 1.0;
            var denom = Log2(i + 2);
            sum += gain / denom;
        }

        return sum;
    }

    private static double IdealDcgAtK(IReadOnlyDictionary<string, int> relByPath, int k)
    {
        // sort gains desc
        var gains = new List<int>(relByPath.Values);
        gains.Sort((a, b) => b.CompareTo(a));

        double sum = 0.0;
        var n = Math.Min(k, gains.Count);

        for (int i = 0; i < n; i++)
        {
            var rel = gains[i];
            var gain = Math.Pow(2.0, rel) - 1.0;
            var denom = Log2(i + 2);
            sum += gain / denom;
        }

        return sum;
    }

    private static double Log2(double x) => Math.Log(x) / Math.Log(2.0);
}