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
    private readonly LuceneStatsService _luceneStatsService;
    
    private readonly SemaphoreSlim _indexLock = new SemaphoreSlim(1, 1);

    public IndexedSearchService()
    {
        _analyzer = AnalyzerFactory.Create(LuceneVersion.LUCENE_48);

        _luceneIndexService = new LuceneIndexService(_indexPaths, _analyzer);
        _luceneSearchService = new LuceneSearchService(_indexPaths, _analyzer);
        _luceneStatsService = new LuceneStatsService(_indexPaths, _analyzer);
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

        // Load LTR-light model (optional)
        var resourcesDir = Path.Combine(AppContext.BaseDirectory, "Resources");
        var modelPath = Path.Combine(resourcesDir, "ltr_weights.json");
        var model = FileSearchTool.Services.Ranking.LinearModel.TryLoad(modelPath);

        // Analyser query tokens med samme normalisering som rerank (enkelt, men OK)
        var qTokens = FileSearchTool.Services.Ranking.QueryTextUtil.Tokenize(query)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var (N, dfByTerm) = _luceneStatsService.GetDocFreqs(rootFolder, qTokens, field: "content");
        
        foreach (var (path, luceneScore, storedContent, ext, ticks, size, depth, hash) in hits)
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
    
    // Load LTR-light model (optional)
    var resourcesDir = Path.Combine(AppContext.BaseDirectory, "Resources");
    var modelPath = Path.Combine(resourcesDir, "ltr_weights.json");
    var model = FileSearchTool.Services.Ranking.LinearModel.TryLoad(modelPath);

        // Query tokens (samme som rerank bruger)
    var qTokens = FileSearchTool.Services.Ranking.QueryTextUtil.Tokenize(query)
        .Distinct(StringComparer.Ordinal)
        .ToList();

        // DocFreq stats til IDF approx
    var (N, dfByTerm) = _luceneStatsService.GetDocFreqs(rootFolder, qTokens, field: "content");


    // === Stage 2: Re-rank (top 200 -> sort by FinalScore -> take 50) ===
    var scored = new List<SearchResultSummary>(hits.Count);

    // Hvis du udvider SearchResultSummary til at bære hash senere, så group på hash.
    // Lige nu kan du i stedet collapse på "path" (ingen effekt) – bedst at udvide summary med hash.
    foreach (var (path, luceneScore, storedContent, ext, ticks, size, depth, hash) in hits)
    {
        try
        {
            // Use storedContent if present; else fallback extract (slower)
            string text = !string.IsNullOrWhiteSpace(storedContent)
                ? storedContent!
                : TextExtractorFactory.Create(path).ExtractText(path);

            var breakdown = FileSearchTool.Services.Ranking.ReRankScorer
                .ScoreDocument(query, luceneScore, text, ext, ticks, depth, N, dfByTerm, model);


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
                ContainsAllTerms: breakdown.ContainsAllTerms,
                FreshnessScore: breakdown.FreshnessScore,
                FiletypeScore: breakdown.FiletypeScore,
                FolderDepthScore: breakdown.FolderDepthScore,
                EarlyScore: breakdown.EarlyScore,
                CoverageScore: breakdown.CoverageScore
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
                ContainsAllTerms: false,
                FreshnessScore: 0,
                FiletypeScore: 0,
                FolderDepthScore: 0,
                EarlyScore: 0,
                CoverageScore: 0
            ));
        }
    }

    return scored
        .OrderByDescending(x => x.FinalScore)
        .Take(50)
        .ToList();
}


}