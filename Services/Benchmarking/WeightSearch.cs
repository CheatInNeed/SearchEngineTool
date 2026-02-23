using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FileSearchTool.Services.Ranking;

namespace FileSearchTool.Services.Benchmarking;

public static class WeightSearch
{
    public static async Task RunRandomSearchAsync(
        string benchmarkJsonPath,
        string baseWeightsPath,
        int iterations = 200,
        double sigma = 0.15)
    {
        if (!File.Exists(baseWeightsPath))
            throw new FileNotFoundException("Base weights not found.", baseWeightsPath);

        var baseJson = await File.ReadAllTextAsync(baseWeightsPath);
        var baseDict = JsonSerializer.Deserialize<Dictionary<string, float>>(baseJson)
                       ?? throw new Exception("Could not parse ltr_weights.json");

        var runner = new BenchmarkRunner();

        // baseline
        var baselineModel = LinearModel.FromWeights(baseDict);
        var baselineReport = await runner.RunAsync(benchmarkJsonPath, baselineModel);
        var bestScore = baselineReport.MeanNdcg;
        var best = new Dictionary<string, float>(baseDict);

        Console.WriteLine($"Baseline mean NDCG: {bestScore:0.0000}");

        var rng = new Random(1337);

        for (int it = 1; it <= iterations; it++)
        {
            var cand = Mutate(best, rng, sigma);
            var model = LinearModel.FromWeights(cand);

            var rep = await runner.RunAsync(benchmarkJsonPath, model);
            var score = rep.MeanNdcg;

            if (score > bestScore)
            {
                bestScore = score;
                best = cand;

                Console.WriteLine($"[iter {it}] NEW BEST mean NDCG: {bestScore:0.0000}");
            }
        }

        var outPath = Path.Combine(Path.GetDirectoryName(baseWeightsPath) ?? ".", "ltr_weights.tuned.json");
        var outJson = JsonSerializer.Serialize(best, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(outPath, outJson);

        Console.WriteLine($"Wrote tuned weights to: {outPath}");
        Console.WriteLine($"Best mean NDCG: {bestScore:0.0000}");
    }

    private static Dictionary<string, float> Mutate(Dictionary<string, float> w, Random rng, double sigma)
    {
        var d = new Dictionary<string, float>(w);

        foreach (var key in d.Keys.ToList())
        {
            var noise = NextGaussian(rng) * sigma;
            d[key] = (float)(d[key] * (1.0 + noise));
        }

        return d;
    }

    // Box-Muller
    private static double NextGaussian(Random rng)
    {
        var u1 = 1.0 - rng.NextDouble();
        var u2 = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}