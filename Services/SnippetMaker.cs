using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FileSearchTool.Models;
using System.Text.RegularExpressions;

namespace FileSearchTool.Services;

public static class SnippetMaker
{
    // TODO: Ny metode der returnerer flere snippets pr dokument
    private static bool IsSingleToken(string p) => !p.Contains(' ');
    public static List<string> MakeSnippets(
        string fullText,
        string rawQuery,
        IEnumerable<string> matchedTerms,
        int maxSnippets = 5,
        int windowSize = 60
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
            if (IsSingleToken(pattern))
            {
                var re = BuildWholeWordRegex(pattern);
                foreach (Match m in re.Matches(normalizedFullText))
                {
                    hits.Add((m.Index, m.Length, pattern));
                }
            }
            else
            {
                // Phrase: behold substring-match
                int startIndex = 0;
                while (true)
                {
                    var idx = normalizedFullText.IndexOf(pattern, startIndex, StringComparison.Ordinal);
                    if (idx < 0) break;

                    hits.Add((idx, pattern.Length, pattern));
                    startIndex = idx + pattern.Length;
                }
            }
        }
        
        if (hits.Count == 0)
            return new List<string>();
        
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
                var pat = p.Trim();
                if (pat.Length == 0) continue;

                if (IsSingleToken(pat))
                {
                    var re = BuildWholeWordRegex(pat);
                    snippet = re.Replace(snippet, m => $"[{m.Value}]");
                }
                else
                {
                    // Phrase highlight (ok som substring)
                    snippet = snippet.Replace(pat, $"[{pat}]", StringComparison.Ordinal);
                }
            }

            // (Valgfrit) Undgå duplikat-snippets hvis mange clusters ender i samme window
            if (!snippets.Contains(snippet, StringComparer.Ordinal))
                snippets.Add(snippet);
        }
        return snippets;
    }
    private static Regex BuildWholeWordRegex(string token)
    {
        // \b virker ok her fordi du allerede lowercaser teksten og tokens er simple.
        // Escape så specialtegn ikke ødelægger regex.
        return new Regex(@"\b" + Regex.Escape(token) + @"\b", RegexOptions.Compiled);
    }

}

