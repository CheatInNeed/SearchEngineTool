using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileSearchTool.Models;

namespace FileSearchTool.Services;

public class NaiveSearchService
{
    private readonly FileCrawler _crawler = new();

    public Task<List<SearchResult>> SearchAsync(string rootFolder, string query)
    {
        // Kør den "tunge" søgning på en baggrundstråd så UI ikke fryser.
        return Task.Run(() =>
        {
            var results = new List<SearchResult>();

            if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
                return results;

            if (string.IsNullOrWhiteSpace(query))
                return results;

            foreach (var file in _crawler.GetFiles(rootFolder))
            {
                try
                {
                    var text = File.ReadAllText(file);

                    if (text.Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        // Lav et ultra simpelt snippet:
                        var snippet = MakeSnippet(text, query);
                        results.Add(new SearchResult(file, snippet));
                    }
                }
                catch
                {
                    // Ignorér filer vi ikke kan læse (permissions, encoding, osv.)
                }
            }

            return results;
        });
    }

    private static string MakeSnippet(string text, string query)
    {
        var idx = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return "";

        var start = Math.Max(0, idx - 40);
        var length = Math.Min(text.Length - start, 120);

        var snippet = text.Substring(start, length).Replace("\r", " ").Replace("\n", " ");
        return snippet;
    }
}