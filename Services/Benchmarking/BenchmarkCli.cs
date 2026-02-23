using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FileSearchTool.Services.Benchmarking;

public static class BenchmarkCli
{
    public static async Task Run(string[] args)
    {
        // find benchmark path after --bench
        var idx = Array.FindIndex(args, a => string.Equals(a, "--bench", StringComparison.OrdinalIgnoreCase));
        string? path = null;

        if (idx >= 0 && idx + 1 < args.Length)
            path = args[idx + 1];

        if (string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run -- --bench <path-to-benchmark.json>");
            return;
        }

        if (!File.Exists(path))
        {
            Console.WriteLine($"Benchmark file not found: {path}");
            return;
        }

        var runner = new BenchmarkRunner();
        var report = await runner.RunAsync(path);

        Console.WriteLine();
        Console.WriteLine($"Benchmark: {Path.GetFileName(path)}");
        Console.WriteLine($"RootFolder: {report.RootFolder}");
        Console.WriteLine($"K: {report.K}");
        Console.WriteLine();

        foreach (var q in report.PerQuery.OrderByDescending(x => x.NdcgAtK))
        {
            Console.WriteLine($"[{q.Id}]");
            Console.WriteLine($"  Query: {q.Query}");
            Console.WriteLine($"  NDCG@{report.K}: {q.NdcgAtK:0.0000}");
            Console.WriteLine($"  MRR:      {q.Mrr:0.0000}");
            Console.WriteLine($"  retrieved: {q.Retrieved} | relevant: {q.RelevantCount}");
            Console.WriteLine();
        }

        Console.WriteLine("== Summary ==");
        Console.WriteLine($"Mean NDCG@{report.K}: {report.MeanNdcg:0.0000}");
        Console.WriteLine($"Mean MRR:        {report.MeanMrr:0.0000}");
        Console.WriteLine();
    }
}