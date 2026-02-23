using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FileSearchTool.Services.Benchmarking;

public sealed class BenchmarkSpec
{
    [JsonPropertyName("rootFolder")]
    public string RootFolder { get; set; } = "";

    [JsonPropertyName("k")]
    public int K { get; set; } = 10;

    [JsonPropertyName("queries")]
    public List<BenchmarkQuery> Queries { get; set; } = new();
}

public sealed class BenchmarkQuery
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("query")]
    public string Query { get; set; } = "";

    // relevant docs with optional graded gain (default 1)
    [JsonPropertyName("relevant")]
    public List<RelevantDoc> Relevant { get; set; } = new();
}

public sealed class RelevantDoc
{
    // Recommended: relative path from rootFolder (but absolute is accepted)
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("gain")]
    public int Gain { get; set; } = 1;
}