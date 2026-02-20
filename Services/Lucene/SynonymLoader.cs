using System;
using System.IO;
using System.Linq;
using System.Text;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Synonym;
using Lucene.Net.Util;

namespace FileSearchTool.Services.Lucene;

public static class SynonymLoader
{
    public static SynonymMap? TryLoad(LuceneVersion version, string synonymsPath)
    {
        try
        {
            if (!File.Exists(synonymsPath))
                return null;

            var builder = new SynonymMap.Builder(dedup: true);

            var lines = File.ReadAllLines(synonymsPath, Encoding.UTF8)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0 && !x.StartsWith("#"));

            foreach (var line in lines)
            {
                // Format: a, b, c
                var parts = line.Split(',')
                    .Select(x => x.Trim())
                    .Where(x => x.Length > 0)
                    .ToArray();

                if (parts.Length < 2) continue;

                // Bidirectional synonyms mellem alle par
                for (int i = 0; i < parts.Length; i++)
                for (int j = 0; j < parts.Length; j++)
                {
                    if (i == j) continue;
                    builder.Add(new CharsRef(parts[i]), new CharsRef(parts[j]), includeOrig: true);
                }
            }

            return builder.Build();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Lucene.Net 4.8 kan have SynonymGraphFilter eller SynonymFilter afhængigt af build.
    /// Vi vælger med reflection for at undgå compile-breaks.
    /// </summary>
    public static TokenStream ApplySynonymsOrFallback(TokenStream input, SynonymMap map)
    {
        try
        {
            // 1) prøv SynonymGraphFilter
            var graphType = FindType("Lucene.Net.Analysis.Synonym.SynonymGraphFilter");
            if (graphType != null)
            {
                return (TokenStream)Activator.CreateInstance(graphType, input, map, true)!;
            }

            // 2) fallback SynonymFilter
            var filterType = FindType("Lucene.Net.Analysis.Synonym.SynonymFilter");
            if (filterType != null)
            {
                return (TokenStream)Activator.CreateInstance(filterType, input, map, true)!;
            }
        }
        catch
        {
            // ignore
        }

        return input;
    }

    private static Type? FindType(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(fullName);
            if (t != null) return t;
        }
        return null;
    }
}
