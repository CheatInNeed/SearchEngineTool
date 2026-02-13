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
                    var termMatch = terms.Any(t => text.Contains(t, StringComparison.OrdinalIgnoreCase));

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
                        var normalizedTerms = terms
                            .Where(t => !string.IsNullOrWhiteSpace(t))
                            .Select(t => t.Trim())
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();

                        var matchedTerms = new List<string>();

                        if (phraseMatch)
                        {
                            matchedTerms.AddRange(normalizedTerms);
                        }
                        else
                        {
                            foreach (var term in normalizedTerms)
                            {
                                if (text.Contains(term, StringComparison.OrdinalIgnoreCase))
                                {
                                    matchedTerms.Add(term);
                                }
                            }
                        }
                        
                        matchedTerms = matchedTerms
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        
                        
                        var snippets = SnippetMaker.MakeSnippets(text, query, matchedTerms);

                        for (int i = 0; i < snippets.Count; i++)
                        {
                            results.Add(new SearchResult(file, snippets[i], i + 1));
                        }
                    }
                }
                catch
                {
                    
                }
            }

            return results;
        });
    }
}