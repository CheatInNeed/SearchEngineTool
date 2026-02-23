using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FileSearchTool.Services.Ranking;

namespace FileSearchTool.Services.Benchmarking;

public sealed class BenchmarkRunner
{
    private readonly FileSearchTool.Services.IndexedSearchService _search = new();

    public async Task<BenchmarkReport> RunAsync(string benchmarkJsonPath, LinearModel? modelOverride = null)
    {
        if (string.IsNullOrWhiteSpace(benchmarkJsonPath))
            throw new ArgumentException("benchmarkJsonPath is empty.");

        if (!File.Exists(benchmarkJsonPath))
            throw new FileNotFoundException("Benchmark file not found.", benchmarkJsonPath);

        var json = await File.ReadAllTextAsync(benchmarkJsonPath);
        var spec = JsonSerializer.Deserialize<BenchmarkSpec>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new Exception("Could not parse benchmark JSON.");

        var root = spec.RootFolder?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            throw new DirectoryNotFoundException($"RootFolder does not exist: {root}");

        // Ensure index exists/up-to-date
        await _search.BuildIndexAsync(root);

        var perQuery = new List<PerQueryResult>();
        int k = spec.K <= 0 ? 10 : spec.K;

        foreach (var q in spec.Queries)
        {
            var qid = string.IsNullOrWhiteSpace(q.Id) ? q.Query : q.Id!;
            var rel = BuildRelDict(root, q);

            var results = await _search.SearchSummaryAsync(root, q.Query, modelOverride);
            var ranked = results.Select(r => NormalizePath(r.FilePath)).ToList();

            var ndcg = Metrics.NdcgAtK(ranked, rel, k);
            var mrr = Metrics.Mrr(ranked, rel);

            perQuery.Add(new PerQueryResult
            {
                Id = qid,
                Query = q.Query,
                NdcgAtK = ndcg,
                Mrr = mrr,
                Retrieved = ranked.Count,
                RelevantCount = rel.Count
            });
        }

        return new BenchmarkReport
        {
            RootFolder = root,
            K = k,
            PerQuery = perQuery
        };
    }

    private static Dictionary<string, int> BuildRelDict(string rootFolder, BenchmarkQuery q)
    {
        var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in q.Relevant)
        {
            if (string.IsNullOrWhiteSpace(r.Path)) continue;

            var full = r.Path;
            if (!Path.IsPathRooted(full))
                full = Path.Combine(rootFolder, r.Path);

            full = NormalizePath(full);

            var gain = r.Gain <= 0 ? 1 : r.Gain;
            d[full] = gain;
        }

        return d;
    }

    private static string NormalizePath(string p)
    {
        try { return Path.GetFullPath(p); }
        catch { return p; }
    }
}

public sealed class BenchmarkReport
{
    public string RootFolder { get; set; } = "";
    public int K { get; set; } = 10;
    public List<PerQueryResult> PerQuery { get; set; } = new();

    public double MeanNdcg => PerQuery.Count == 0 ? 0.0 : PerQuery.Average(x => x.NdcgAtK);
    public double MeanMrr => PerQuery.Count == 0 ? 0.0 : PerQuery.Average(x => x.Mrr);
}

public sealed class PerQueryResult
{
    public string Id { get; set; } = "";
    public string Query { get; set; } = "";
    public double NdcgAtK { get; set; }
    public double Mrr { get; set; }
    public int Retrieved { get; set; }
    public int RelevantCount { get; set; }
}