using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FileSearchTool.Models;

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
    
    // TODO: Ny metode der returnerer flere snippets pr dokument
    public static List<string> MakeSnippets(
        string fullText,
        string rawQuery,
        IEnumerable<string> matchedTerms,
        int maxSnippets = 5,
        int windowSize = 240
    )
    {
        if (string.IsNullOrWhiteSpace(fullText) ||
            string.IsNullOrWhiteSpace(rawQuery) ||
            matchedTerms == null)
        {
            return new List<string>();
        }

        var normalizedFullText = fullText.ToLowerInvariant();
        normalizedFullText = normalizedFullText
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\t", " ");

        while (normalizedFullText.Contains("  "))
        {
            normalizedFullText = normalizedFullText.Replace("  ", " ");
        }
        normalizedFullText = normalizedFullText.Trim();
        
        var patterns = new List<string>();
        var normalizedQuery = rawQuery.Trim().ToLowerInvariant();
        
        normalizedQuery = normalizedQuery.
            Replace("\r", " ").
            Replace("\n", " ").
            Replace("\t", " ");
        while (normalizedQuery.Contains("  "))
        {
            normalizedQuery = normalizedQuery.Replace("  ", " ");
        }
        normalizedQuery = normalizedQuery.Trim();
        
        var queryTokens = normalizedQuery
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (queryTokens.Length >= 2)
        {
            patterns.Add(normalizedQuery);
        }
        
        foreach (var term in matchedTerms)
        {
            if (string.IsNullOrWhiteSpace(term)) continue;

            var t = term.Trim().ToLowerInvariant();
            if (t.Length == 0) continue;

            patterns.Add(t);
        }

        patterns = patterns
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        
        if (patterns.Count == 0) 
            return new List<string>();

        var hits = new List<(int Start, int Length, string Pattern)>();
        
        foreach (var pattern in patterns)
        {
            int startIndex = 0;

            while (true)
            {
                var idx = normalizedFullText.IndexOf(pattern, startIndex, StringComparison.Ordinal);

                if (idx < 0)
                    break;

                hits.Add((idx, pattern.Length, pattern));
                startIndex = idx + pattern.Length;
            }
        }
        
        // 1) Sortér hits på startIndex
        hits = hits
            .OrderBy(h => h.Start)
            .ToList();

        var clusters = new List<List<(int Start, int Length, string Pattern)>>();

        List<(int Start, int Length, string Pattern)>? current = null;
        int currentRightEdge = -1;

        foreach (var hit in hits)
        {
            if (current == null)
            {
                // start første cluster
                current = new List<(int Start, int Length, string Pattern)> { hit };
                clusters.Add(current);
                currentRightEdge = hit.Start + hit.Length;
                continue;
            }

            // afstand fra "højre kant" af nuværende cluster til næste hit
            var gap = hit.Start - currentRightEdge;

            if (gap <= windowSize)
            {
                // tæt nok -> samme cluster
                current.Add(hit);
                currentRightEdge = Math.Max(currentRightEdge, hit.Start + hit.Length);
            }
            else
            {
                // for langt væk -> ny cluster
                current = new List<(int Start, int Length, string Pattern)> { hit };
                clusters.Add(current);
                currentRightEdge = hit.Start + hit.Length;
            }
        }
        
        
        var snippets = new List<string>();

        // (Valgfrit men anbefalet) Sortér clusters efter hvor mange hits de har (bedste først)
        // Hvis du hellere vil have “i dokumentrækkefølge”, så fjern OrderByDescending og behold bare clusters.
        var orderedClusters = clusters
            .OrderByDescending(c => c.Count)
            .ToList();

        foreach (var cluster in orderedClusters)
        {
            if (snippets.Count >= maxSnippets)
                break;

            // Cluster bounds
            var clusterStart = cluster.Min(h => h.Start);
            var clusterEnd = cluster.Max(h => h.Start + h.Length);

            // Center window omkring cluster
            var center = (clusterStart + clusterEnd) / 2;

            var start = Math.Max(0, center - (windowSize / 2));
            var end = Math.Min(normalizedFullText.Length, start + windowSize);

            // Hvis vi rammer slutningen, så skub start tilbage for at få fuldt vindue (når muligt)
            start = Math.Max(0, end - windowSize);

            var snippet = normalizedFullText.Substring(start, end - start).Trim();

            // Prefix/suffix "..." hvis vi klipper midt i teksten
            if (start > 0)
                snippet = "..." + snippet;
            if (end < normalizedFullText.Length)
                snippet = snippet + "...";

            // Normalisér whitespace igen (sikkerhed)
            snippet = snippet.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
            while (snippet.Contains("  "))
                snippet = snippet.Replace("  ", " ");
            snippet = snippet.Trim();

            // Highlight: marker matched terms (og phrase hvis den er med i patterns)
            // NOTE: simpelt approach med Replace (case-sensitive fordi snippet er lowercase)
            foreach (var p in patterns)
            {
                if (string.IsNullOrWhiteSpace(p)) continue;

                // Undgå at highlight tomme strings
                var pat = p.Trim();
                if (pat.Length == 0) continue;

                // Marker med [term]
                snippet = snippet.Replace(pat, $"[{pat}]", StringComparison.Ordinal);
            }

            // (Valgfrit) Undgå duplikat-snippets hvis mange clusters ender i samme window
            if (!snippets.Contains(snippet, StringComparer.Ordinal))
                snippets.Add(snippet);
        }
        return snippets;
    }


}

