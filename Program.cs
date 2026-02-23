using System;
using Avalonia;

namespace FileSearchTool;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // CLI mode: benchmark
        if (args != null && Array.Exists(args, a => string.Equals(a, "--bench", StringComparison.OrdinalIgnoreCase)))
        {
            FileSearchTool.Services.Benchmarking.BenchmarkCli
                .Run(args)
                .GetAwaiter()
                .GetResult();
            return;
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}