using System.Collections.Generic;
using System.IO;

namespace FileSearchTool.Models;

public record SearchResultSummary(
    string FilePath,
    float TotalScore,
    int PhraseHits,
    int ExactHits,
    int InfixHits)
{
    public string FileName => Path.GetFileName(FilePath);

    public string HitSummary
    {
        get
        {
            // Vis kun dem der er >0
            var parts = new List<string>();
            if (PhraseHits > 0) parts.Add($"{PhraseHits}× phrase (12x)");
            if (ExactHits > 0) parts.Add($"{ExactHits}× exact (6x)");
            if (InfixHits > 0) parts.Add($"{InfixHits}× infix (2.5x)");
            if (parts.Count == 0) return "0 hits";
            return string.Join("  ·  ", parts);
        }
    }
}