using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileSearchTool.Models;
using FileSearchTool.Services.TextExtraction;

namespace FileSearchTool.Services;

public class NaiveSearchService
{
    private readonly FileCrawler _crawler = new();

    public Task<List<SearchResult>> SearchAsync(string rootFolder, string query)
    {
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
                    var extractor = TextExtractorFactory.Create(file);
                    var text = extractor.ExtractText(file);
                    var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    

                    var phraseMatch = text.Contains(query, StringComparison.OrdinalIgnoreCase);

                    var termMatch = false;

                    if (!phraseMatch)
                    {
                        foreach (var term in terms)
                        {
                            if (text.Contains(term, StringComparison.OrdinalIgnoreCase))
                            {
                                termMatch = true;
                                break;
                            }
                        }
                    }

                    if (phraseMatch || termMatch)
                    {
                        var snippet = SnippetMaker.MakeSnippet(text, terms);
                        results.Add(new SearchResult(file, snippet));
                    }
                }
                catch
                {
                    
                }
            }

            return results;
        });
    }

    private static string MakeSnippet(string text, string query)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(query))
            return "";

        // Prøv først at finde hele frasen
        var idx = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);

        // Hvis ikke fundet, fallback til første ord i query
        if (idx < 0)
        {
            var firstWord = query.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (firstWord == null) return "";

            idx = text.IndexOf(firstWord, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return "";
            query = firstWord;
        }

        // Bestem snippet-område
        const int context = 60;

        var start = Math.Max(0, idx - context);
        var end = Math.Min(text.Length, idx + query.Length + context);

        var snippet = text.Substring(start, end - start);

        // Normalisér whitespace
        snippet = snippet.Replace("\r", " ").Replace("\n", " ");
        while (snippet.Contains("  "))
            snippet = snippet.Replace("  ", " ");

        snippet = snippet.Trim();

        // Highlight match (simpel version)
        snippet = snippet.Replace(
            query,
            $"**{query}**",
            StringComparison.OrdinalIgnoreCase
        );

        return snippet;
    }
}