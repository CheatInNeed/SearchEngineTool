using System;

namespace FileSearchTool.Services.Ranking;

public static class MetadataFeatures
{
    // Log-decay freshness (nyere => højere)
    public static float FreshnessBoost(long nowTicks, long docTicks)
    {
        if (docTicks <= 0) return 0f;
        var ageTicks = Math.Max(0, nowTicks - docTicks);
        var ageDays = ageTicks / (double)TimeSpan.TicksPerDay;

        // 0 dage ~ 1.0, 7 dage ~ ~0.5, 30 dage ~ lavere
        var score = 1.0 / (1.0 + Math.Log10(1.0 + ageDays));
        return (float)score;
    }

    public static float ExtensionPrior(string ext)
    {
        if (string.IsNullOrWhiteSpace(ext)) return 0f;
        ext = ext.ToLowerInvariant();

        // tune efter din case
        return ext switch
        {
            ".pdf" => 0.6f,
            ".txt" => 0.3f,
            _ => 0.1f
        };
    }

    public static float FolderDepthPrior(int depth)
    {
        // lav depth => lidt boost (top-level docs)
        if (depth <= 0) return 0.4f;
        if (depth == 1) return 0.2f;
        if (depth == 2) return 0.1f;
        return 0f;
    }
}