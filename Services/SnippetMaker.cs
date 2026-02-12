using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FileSearchTool.Services;

public static class SnippetMaker
{
    public static string MakeLineSnippet(string[] lines, IEnumerable<string> terms)
    {
        if (lines == null || lines.Length <= 0 || terms == null)
        {
            return "";
        }

        var normalizedTerms = new List<string>();

        foreach (var term in terms)
        {
            if (string.IsNullOrWhiteSpace(term)) continue;

            var normalized = term.ToLowerInvariant();

            if (!normalizedTerms.Contains(normalized))
            {
                normalizedTerms.Add(normalized);
            }
        }

        if (normalizedTerms.Count == 0)
            return "";
        
        var matchline = -1;
        
        // TODO 1: Find første linje der indeholder mindst ét term (case-insensitive)
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            foreach (var term in normalizedTerms)
            {
                if (line.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matchline = i;
                    break;
                }
            }

            if (matchline != -1)
                break;
        }

        if (matchline < 0)
            return "";
        
        var start = Math.Max(0, matchline - 1);
        var end = Math.Min(lines.Length - 1, start + 2);

        var sb = new StringBuilder();

        for (int i = start; i <= end; i++)
        {
            var part = lines[i].Trim();
            if (part.Length == 0) continue;

            if (sb.Length > 0)
                sb.Append(" | ");

            sb.Append(part);
        }

        var snippet = sb.ToString();
        
        var max = 80;
        if (snippet.Length > max)
        {
            snippet = snippet.Substring(0, max - 3).TrimEnd() + "...";
        }

        // TODO 6: Highlight (valgfri)
        // - marker termer i snippet (start simpelt)

        return snippet;
    }
    
    public static string MakeSnippet(string fullText, IEnumerable<string> terms)
    {
        // TODO 2: Guard
        if (string.IsNullOrWhiteSpace(fullText) || terms == null)
            return "";

        // TODO 3: Normalisér terms (lowercase, unique, drop empty)
        var normalized = terms
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
            return "";

        // TODO 4: Find første match-position i fullText (case-insensitive)
        var matchIdx = -1;
        var matchTerm = "";

        foreach (var term in normalized)
        {
            var idx = fullText.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0 && (matchIdx == -1 || idx < matchIdx))
            {
                matchIdx = idx;
                matchTerm = term;
            }
        }

        if (matchIdx < 0)
            return "";

        // TODO 5: Klip et udsnit omkring match
        const int before = 60;
        const int after = 120;

        var start = Math.Max(0, matchIdx - before);
        var end = Math.Min(fullText.Length, matchIdx + matchTerm.Length + after);

        var snippet = fullText.Substring(start, end - start);

        // TODO 6: Normalisér whitespace
        snippet = snippet.Replace("\r", " ").Replace("\n", " ");
        while (snippet.Contains("  "))
            snippet = snippet.Replace("  ", " ");

        return snippet.Trim();
    }

}

// TODO 1: Lav en ny overload:
//   MakeSnippet(string fullText, IEnumerable<string> terms)
// som:
// :contentReference[oaicite:19]{index=19} (et af terms) i fullText (case-insensitive)
// - returnerer et udsnit omkring match (fx 60 chars før og 120 efter)
// - erstatter \r/\n med mellemrum

// TODO 2: (valgfrit) Hvis brugeren skrev flere ord, så prioritér at finde hele frasen først
// TODO 3: (valgfrit) Highlight: marker hits ved at sætte fx [term] eller **term** (UI senere)