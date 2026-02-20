using System;
using System.Collections.Generic;
using System.IO;
using Lucene.Net.Analysis;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;

namespace FileSearchTool.Services.Lucene;

public class LuceneSearchService
{
    private const string FieldPath = "path";
    private const string FieldContent = "content"; // indexed (normaliseret)
    private const string FieldContentStored = "contentStored"; // original til snippets

    private readonly LuceneIndexPaths _indexPaths;
    private readonly LuceneQueryBuilder _queryBuilder;
    private readonly LuceneVersion _version = LuceneVersion.LUCENE_48;

    public LuceneSearchService(LuceneIndexPaths indexPaths, Analyzer analyzer)
    {
        _indexPaths = indexPaths ?? throw new ArgumentNullException(nameof(indexPaths));
        if (analyzer == null) throw new ArgumentNullException(nameof(analyzer));

        _queryBuilder = new LuceneQueryBuilder(analyzer);
    }

    /// <summary>
    /// Searches the Lucene index for the given folder and query.
    /// Returns (path, score, storedContent) where storedContent may be null if not stored in the index.
    /// </summary>
    public List<(string path, float score, string? storedContent)> Search(
        string rootFolder,
        string rawQuery,
        int topN = 200)
    {
        var results = new List<(string path, float score, string? storedContent)>();

        if (string.IsNullOrWhiteSpace(rootFolder))
            return results;

        if (string.IsNullOrWhiteSpace(rawQuery))
            return results;

        try
        {
            var indexDirPath = _indexPaths.GetIndexDirectory(rootFolder);

            // If no index exists, return empty
            if (!System.IO.Directory.Exists(indexDirPath)) 
                return results;

            using var dir = FSDirectory.Open(new DirectoryInfo(indexDirPath));

            if (!DirectoryReader.IndexExists(dir))
                return results;

            using var reader = DirectoryReader.Open(dir);
            
            var searcher = new IndexSearcher(reader);

            var query = _queryBuilder.Build(rawQuery);

            var topDocs = searcher.Search(query, topN);

            foreach (var sd in topDocs.ScoreDocs)
            {
                var doc = searcher.Doc(sd.Doc);

                var path = doc.Get(FieldPath);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                // storedContent is only available if you indexed Field.Store.YES on "content"
                var content = doc.Get(FieldContentStored);

                results.Add((path, sd.Score, content));
            }
        }
        catch
        {
            // For now: swallow (search should not crash UI)
            // Later: log errors, show status message, etc.
        }

        return results;
    }
}
