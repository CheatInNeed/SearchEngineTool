using System;
using System.Collections.Generic;
using System.IO;
using Lucene.Net.Analysis;
using Lucene.Net.Index;
using Lucene.Net.Store;

namespace FileSearchTool.Services.Lucene;

public sealed class LuceneStatsService
{
    private readonly LuceneIndexPaths _indexPaths;
    private readonly Analyzer _analyzer;

    public LuceneStatsService(LuceneIndexPaths indexPaths, Analyzer analyzer)
    {
        _indexPaths = indexPaths;
        _analyzer = analyzer;
    }

    public (int maxDoc, Dictionary<string,int> dfByTerm) GetDocFreqs(string rootFolder, IEnumerable<string> analyzedTerms, string field = "content")
    {
        var df = new Dictionary<string, int>(StringComparer.Ordinal);

        var indexDirPath = _indexPaths.GetIndexDirectory(rootFolder);
        using var dir = FSDirectory.Open(new DirectoryInfo(indexDirPath));
        if (!DirectoryReader.IndexExists(dir))
            return (0, df);

        using var reader = DirectoryReader.Open(dir);
        var maxDoc = reader.MaxDoc;

        foreach (var t in analyzedTerms)
        {
            if (string.IsNullOrWhiteSpace(t)) continue;
            var term = new Term(field, t);
            df[t] = reader.DocFreq(term);
        }

        return (maxDoc, df);
    }
}