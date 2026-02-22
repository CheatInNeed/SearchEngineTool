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
    private const string FieldExt = "ext";
    private const string FieldLastModifiedTicks = "lastModifiedTicks";
    private const string FieldFileSizeBytes = "fileSizeBytes";
    private const string FieldFolderDepth = "folderDepth";
    private const string FieldContentHash = "contentHash";
    private const string FieldParentPath = "parentPath";
    private const string FieldChunkIndex = "chunkIndex";


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
    public List<(string path, float score, string? storedContent, string ext, long lastModifiedTicks, long fileSizeBytes, int folderDepth, string contentHash)> Search(
        string rootFolder,
        string rawQuery,
        int topN = 200)
    {
        var results = new List<(string path, float score, string? storedContent, string ext, long lastModifiedTicks, long fileSizeBytes, int folderDepth, string contentHash)>();

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

                var parentPath = doc.Get(FieldParentPath);
                var path = string.IsNullOrWhiteSpace(parentPath) ? doc.Get(FieldPath) : parentPath;
                var chunkField = doc.GetField(FieldChunkIndex);
                var chunkIndex = chunkField?.GetInt32Value() ?? 0;
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                var content = doc.Get(FieldContentStored);

                var ext = doc.Get(FieldExt) ?? "";
                long.TryParse(doc.Get(FieldLastModifiedTicks) ?? "0", out var ticks);
                long.TryParse(doc.Get(FieldFileSizeBytes) ?? "0", out var size);
                int.TryParse(doc.Get(FieldFolderDepth) ?? "0", out var depth);
                var hash = doc.Get(FieldContentHash) ?? "";

                results.Add((path, sd.Score, content, ext, ticks, size, depth, hash));
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
