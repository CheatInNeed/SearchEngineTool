using System;
using System.Threading.Tasks;
using FileSearchTool.Services.Lucene;
using FileSearchTool.Services.TextExtraction;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Util;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileSearchTool.Models;
using FileSearchTool.Services.TextExtraction;
using System.Threading;
using Lucene.Net.Analysis.Util;
using Lucene.Net.Analysis.Miscellaneous;

namespace FileSearchTool.Services;

public class IndexedSearchService
{
    private readonly FileCrawler _fileCrawler = new FileCrawler();

    // Delte Lucene dependencies (samme analyzer bruges begge steder!)
    private readonly LuceneIndexPaths _indexPaths = new LuceneIndexPaths();
    private readonly Analyzer _analyzer;

    private readonly LuceneIndexService _luceneIndexService;
    private readonly LuceneSearchService _luceneSearchService;
    
    private readonly SemaphoreSlim _indexLock = new SemaphoreSlim(1, 1);

    public IndexedSearchService()
    {
        // Default analyzer (normal)
        var standard = new StandardAnalyzer(LuceneVersion.LUCENE_48, CharArraySet.EMPTY_SET);

        // Infix analyzer ONLY for "contentInfix"
        var perField = new Dictionary<string, Analyzer>
        {
            { "contentInfix", new FileSearchTool.Services.Lucene.InfixNGramAnalyzer(LuceneVersion.LUCENE_48, 3, 15) }
        };

        _analyzer = new PerFieldAnalyzerWrapper(standard, perField);

        _luceneIndexService = new LuceneIndexService(_indexPaths, _analyzer);
        _luceneSearchService = new LuceneSearchService(_indexPaths, _analyzer);
    }
    
    // We use this to know if rootFolder already exists.
    private string? _indexedRootFolder;
    
    public async Task BuildIndexAsync(string rootFolder)
    {
        if (string.IsNullOrWhiteSpace(rootFolder))
            return;

        await _indexLock.WaitAsync();
        try
        {
            var files = _fileCrawler.GetFiles(rootFolder).ToList();
            var fileSet = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);

            // Hent ticks fra index (hvis findes)
            var indexedTicks = _luceneIndexService.IndexExists(rootFolder)
                ? _luceneIndexService.GetIndexedLastModifiedTicks(rootFolder)
                : new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            var docs = new List<(string path, string content)>();

            foreach (var file in files)
            {
                try
                {
                    long currentTicks = File.GetLastWriteTimeUtc(file).Ticks;

                    // Skip hvis uændret
                    if (indexedTicks.TryGetValue(file, out var oldTicks) && oldTicks == currentTicks)
                        continue;

                    var extractor = TextExtractorFactory.Create(file);
                    var text = extractor.ExtractText(file);

                    if (!string.IsNullOrWhiteSpace(text))
                        docs.Add((file, text));
                }
                catch
                {
                    // swallow per file
                }
            }

            // Opdatér kun changed docs (BuildOrUpdate er allerede incremental)
            if (docs.Count > 0)
                await _luceneIndexService.BuildOrUpdateIndexAsync(rootFolder, docs);

            // Slet docs der ikke findes mere på disk
            if (_luceneIndexService.IndexExists(rootFolder))
                _luceneIndexService.DeleteDocumentsMissingOnDisk(rootFolder, fileSet);

            _indexedRootFolder = rootFolder;
        }
        finally
        {
            _indexLock.Release();
        }
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
    
    public async Task<List<SearchResultSummary>> SearchSummaryAsync(string rootFolder, string query)
{
    var results = new List<SearchResultSummary>();

    if (string.IsNullOrWhiteSpace(rootFolder) || string.IsNullOrWhiteSpace(query))
        return results;

    if (_indexedRootFolder != rootFolder || !_luceneIndexService.IndexExists(rootFolder))
    {
        await BuildIndexAsync(rootFolder);
        _indexedRootFolder = rootFolder;
    }

    // === Stage 1: Retrieval (top 200) ===
    var hits = _luceneSearchService.Search(rootFolder, query, topN: 200);

    // === Stage 2: Re-rank (top 200 -> sort by FinalScore -> take 50) ===
    var scored = new List<SearchResultSummary>(hits.Count);

    foreach (var (path, luceneScore, storedContent) in hits)
    {
        try
        {
            // Use storedContent if present; else fallback extract (slower)
            string text = !string.IsNullOrWhiteSpace(storedContent)
                ? storedContent!
                : TextExtractorFactory.Create(path).ExtractText(path);

            var breakdown = FileSearchTool.Services.Ranking.ReRankScorer
                .ScoreDocument(query, luceneScore, text);

            scored.Add(new SearchResultSummary(
                FilePath: path,
                FinalScore: breakdown.FinalScore,
                LuceneScore: breakdown.LuceneScore,
                ProximityScore: breakdown.ProximityScore,
                PhraseBonus: breakdown.PhraseBonus,
                DensityScore: breakdown.DensityScore,
                AllTermsBonus: breakdown.AllTermsBonus,
                SpanLength: breakdown.SpanLength,
                MatchedDistinctTerms: breakdown.MatchedDistinctTerms,
                ContainsAllTerms: breakdown.ContainsAllTerms
            ));
        }
        catch
        {
            // If re-rank fails for a doc, keep Lucene baseline (don’t drop result)
            scored.Add(new SearchResultSummary(
                FilePath: path,
                FinalScore: luceneScore,
                LuceneScore: luceneScore,
                ProximityScore: 0,
                PhraseBonus: 0,
                DensityScore: 0,
                AllTermsBonus: 0,
                SpanLength: FileSearchTool.Services.Ranking.RankingWeights.MinSpanFallback,
                MatchedDistinctTerms: 0,
                ContainsAllTerms: false
            ));
        }
    }

    return scored
        .OrderByDescending(x => x.FinalScore)
        .Take(50)
        .ToList();
}


}