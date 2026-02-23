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
        
        path = ResolvePath(path);

        if (!File.Exists(path))
        {
            Console.WriteLine($"Benchmark file not found: {path}");
            return;
        }

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
    
    private static string ResolvePath(string input)
    {
        // 1) as-is (absolute or relative to cwd)
        if (File.Exists(input)) return Path.GetFullPath(input);

        // 2) relative to current working directory
        var cwd = Directory.GetCurrentDirectory();
        var fromCwd = Path.Combine(cwd, input);
        if (File.Exists(fromCwd)) return Path.GetFullPath(fromCwd);

        // 3) relative to output directory (bin/.../Resources)
        var baseDir = AppContext.BaseDirectory;
        var fromBase = Path.Combine(baseDir, input);
        if (File.Exists(fromBase)) return Path.GetFullPath(fromBase);

        // 4) common: user passes "FileSearchTool/Resources/.."
        // try stripping leading "FileSearchTool/"
        var trimmed = input.Replace('\\', '/');
        if (trimmed.StartsWith("FileSearchTool/", StringComparison.OrdinalIgnoreCase))
        {
            var stripped = trimmed.Substring("FileSearchTool/".Length);
            var fromCwdStripped = Path.Combine(cwd, stripped);
            if (File.Exists(fromCwdStripped)) return Path.GetFullPath(fromCwdStripped);

            var fromBaseStripped = Path.Combine(baseDir, stripped);
            if (File.Exists(fromBaseStripped)) return Path.GetFullPath(fromBaseStripped);
        }

        return input; // will fail and print with the attempted path
    }
}