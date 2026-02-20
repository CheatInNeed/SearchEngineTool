using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FileSearchTool.Services.Ranking;

public sealed class LinearModel
{
    private readonly Dictionary<string, float> _w;

    private LinearModel(Dictionary<string, float> w) => _w = w;

    public static LinearModel? TryLoad(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, float>>(json);
            return dict == null ? null : new LinearModel(dict);
        }
        catch
        {
            return null;
        }
    }

    public float Score(FeatureVector f)
    {
        float Sum(string name, float value) => _w.TryGetValue(name, out var w) ? w * value : 0f;

        return
            Sum(nameof(f.LuceneScore), f.LuceneScore) +
            Sum(nameof(f.ProximityScore), f.ProximityScore) +
            Sum(nameof(f.PhraseBonus), f.PhraseBonus) +
            Sum(nameof(f.DensityScore), f.DensityScore) +
            Sum(nameof(f.AllTermsBonus), f.AllTermsBonus) +
            Sum(nameof(f.FreshnessScore), f.FreshnessScore) +
            Sum(nameof(f.FiletypeScore), f.FiletypeScore) +
            Sum(nameof(f.FolderDepthScore), f.FolderDepthScore) +
            Sum(nameof(f.EarlyScore), f.EarlyScore) +
            Sum(nameof(f.CoverageScore), f.CoverageScore);
    }
}