using System;
using System.Threading.Tasks;
using FileSearchTool.Services.Lucene;
using FileSearchTool.Services.TextExtraction;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Util;
using System.Collections.Generic;
using System.Linq;
using FileSearchTool.Models;
using FileSearchTool.Services.TextExtraction;

namespace FileSearchTool.Services;

public class IndexedSearchService
{
    private readonly FileCrawler _fileCrawler = new FileCrawler();

    // Delte Lucene dependencies (samme analyzer bruges begge steder!)
    private readonly LuceneIndexPaths _indexPaths = new LuceneIndexPaths();
    private readonly Analyzer _analyzer = new StandardAnalyzer(LuceneVersion.LUCENE_48);

    private readonly LuceneIndexService _luceneIndexService;
    private readonly LuceneSearchService _luceneSearchService;

    public IndexedSearchService()
    {
        _luceneIndexService = new LuceneIndexService(_indexPaths, _analyzer);
        _luceneSearchService = new LuceneSearchService(_indexPaths, _analyzer);
    }
    
    // We use this to know if rootFolder already exists.
    private string? _indexedRootFolder;
    
    public async Task BuildIndexAsync(string rootFolder)
    {
        if (string.IsNullOrWhiteSpace(rootFolder))
            return;
        
        var files = _fileCrawler.GetFiles(rootFolder);

        var docs = new List<(string path, string content)>();
        foreach (var file in files)
        {
            try
            {
                var extractor = TextExtractorFactory.Create(file);
                var text = extractor.ExtractText(file);

                if (!string.IsNullOrWhiteSpace(text))
                {
                    docs.Add((file, text));
                }
            }
            catch (Exception e)
            {
                
            }
        }
        await _luceneIndexService.BuildOrUpdateIndexAsync(rootFolder, docs);
    }

    public async Task<List<SearchResult>> SearchAsync(string rootFolder, string? query)
    {
        var results = new List<SearchResult>();

        if (string.IsNullOrWhiteSpace(rootFolder) || string.IsNullOrWhiteSpace(query))
            return results;

        if (_indexedRootFolder != rootFolder || !_luceneIndexService.IndexExists(rootFolder))
        {
            await BuildIndexAsync(rootFolder);
            _indexedRootFolder = rootFolder;
        }

        var hits = _luceneSearchService.Search(rootFolder, query);

        foreach (var (path, score, storedContent) in hits)
        {
            try
            {
                string text;
                if (!string.IsNullOrWhiteSpace(storedContent))
                {
                    text = storedContent!;
                }
                else
                {
                    var extractor = TextExtractorFactory.Create(path);
                    text = extractor.ExtractText(path);
                }

                var tokens = query
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var snippets = SnippetMaker.MakeSnippets(text, query, tokens);

                for (int i = 0; i < snippets.Count; i++)
                {
                    results.Add(new SearchResult(path, snippets[i], i + 1));
                }
                
            }
            catch (Exception e)
            {
                
            }
        }

        return results;
    }
}