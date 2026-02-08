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

        if (normalizedTerms == null || normalizedTerms.Count == 0)
            return "";
        
        var matchline = -1;
        
        for (int i = 0; i < lines.Length; i++)
        {
            var lineTokens = Tokenizer.Tokenize(lines[i]); // giver allerede lowercase
            var tokenSet = new HashSet<string>(lineTokens);

            foreach (var term in normalizedTerms)
            {
                if (tokenSet.Contains(term))
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
}