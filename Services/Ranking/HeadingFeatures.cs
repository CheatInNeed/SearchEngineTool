using System;
using System.Collections.Generic;
using System.Linq;

namespace FileSearchTool.Services.Ranking;

public static class HeadingFeatures
{
    public static float HeadingBoost(string rawText, IReadOnlyList<string> qTokens)
    {
        if (string.IsNullOrWhiteSpace(rawText) || qTokens.Count == 0) return 0f;

        // Brug ORIGINAL tekst (før Normalize), fordi headings ofte afhænger af linjer
        var lines = rawText
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .Take(60) // kun start af dokumentet + lidt mere
            .ToList();

        if (lines.Count == 0) return 0f;

        float score = 0f;

        foreach (var line in lines)
        {
            if (!LooksLikeHeading(line)) continue;

            var norm = QueryTextUtil.Normalize(line);

            // token coverage i heading
            int matched = 0;
            foreach (var t in qTokens)
            {
                if (t.Length == 0) continue;
                if (norm.Contains(t, StringComparison.Ordinal)) matched++;
            }

            if (matched == 0) continue;

            // stærk bonus hvis mange tokens findes
            float coverage = matched / (float)Math.Max(1, qTokens.Count);

            // ekstra boost hvis tokens ligger tæt i heading-linjen (kort linje => signal)
            float lengthBoost = line.Length <= 80 ? 1.0f : 0.6f;

            score = Math.Max(score, (1.5f + 3.5f * coverage) * lengthBoost);
        }

        // cap
        return Math.Min(score, 5.0f);
    }

    private static bool LooksLikeHeading(string line)
    {
        if (line.Length < 4) return false;
        if (line.Length > 140) return false;

        // Markdown heading
        if (line.StartsWith("#")) return true;

        // All caps-ish
        int letters = line.Count(char.IsLetter);
        if (letters >= 6)
        {
            int upper = line.Count(c => char.IsLetter(c) && char.IsUpper(c));
            if (upper / (float)letters > 0.7f) return true;
        }

        // “Titel-linje” uden punktum, kort, ofte heading
        if (!line.EndsWith(".") && line.Length <= 70) return true;

        return false;
    }
}