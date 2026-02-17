using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Analysis.Util;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.Util;

namespace FileSearchTool.Services.Lucene;

public class LuceneIndexService
{
    private const string FieldPath = "path";
    private const string FieldContent = "content";
    private const string FieldContentStored = "contentStored";
    private const string FieldFilename = "filename";
    private const string FieldExt = "ext";
    private const string FieldLastModifiedTicks = "lastModifiedTicks";

    private readonly LuceneIndexPaths _indexPaths;
    private readonly Analyzer _analyzer;
    private readonly LuceneVersion _version = LuceneVersion.LUCENE_48;

    /// <summary>
    /// Simple default setup: stable index paths + StandardAnalyzer.
    /// Later you can swap to DanishAnalyzer.
    /// </summary>
    public LuceneIndexService()
    {
        _indexPaths = new LuceneIndexPaths();
        _analyzer = new StandardAnalyzer(_version, CharArraySet.EMPTY_SET);
    }

    public LuceneIndexService(LuceneIndexPaths indexPaths, Analyzer analyzer)
    {
        _indexPaths = indexPaths ?? throw new ArgumentNullException(nameof(indexPaths));
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
    }

    /// <summary>
    /// Checks whether a Lucene index exists for the given root folder.
    /// </summary>
    public bool IndexExists(string rootFolder)
    {
        if (string.IsNullOrWhiteSpace(rootFolder))
            return false;

        try
        {
            var indexDirPath = _indexPaths.GetIndexDirectory(rootFolder);
            using var dir = FSDirectory.Open(new DirectoryInfo(indexDirPath));
            return DirectoryReader.IndexExists(dir);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Builds or updates the Lucene index for a given root folder.
    /// Incremental update: skips files where lastModifiedTicks is unchanged.
    /// </summary>
    public Task BuildOrUpdateIndexAsync(string rootFolder, IEnumerable<(string path, string content)> docs)
    {
        return Task.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(rootFolder))
                throw new ArgumentException("rootFolder is required", nameof(rootFolder));
            if (docs == null)
                throw new ArgumentNullException(nameof(docs));

            var indexDirPath = _indexPaths.GetIndexDirectory(rootFolder);

            using var dir = FSDirectory.Open(new DirectoryInfo(indexDirPath));

            // Load existing metadata once (incremental update)
            var existingTicksByPath = LoadExistingLastModifiedTicks(dir);

            var config = new IndexWriterConfig(_version, _analyzer)
            {
                OpenMode = OpenMode.CREATE_OR_APPEND
            };

            using var writer = new IndexWriter(dir, config);

            var changed = false;

            foreach (var (path, content) in docs)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                if (content == null) continue;

                try
                {
                    long currentTicks;
                    try
                    {
                        // If file exists, use its last write time. Otherwise index anyway.
                        currentTicks = File.Exists(path)
                            ? File.GetLastWriteTimeUtc(path).Ticks
                            : 0L;
                    }
                    catch
                    {
                        currentTicks = 0L;
                    }

                    // Incremental: skip if unchanged
                    if (existingTicksByPath.TryGetValue(path, out var oldTicks) && oldTicks == currentTicks)
                        continue;

                    var fileName = Path.GetFileName(path) ?? "";
                    var ext = Path.GetExtension(path) ?? "";

                    var doc = new Document
                    {
                        new StringField(FieldPath, path, Field.Store.YES),
                        new TextField(FieldContent, NormalizeForIndex(content), Field.Store.NO),
                        new StoredField(FieldContentStored, content),
                        new TextField(FieldFilename, fileName, Field.Store.YES),
                        new StringField(FieldExt, ext, Field.Store.YES),

                        // Stored metadata for incremental updates
                        new StoredField(FieldLastModifiedTicks, currentTicks)
                    };

                    writer.UpdateDocument(new Term(FieldPath, path), doc);
                    changed = true;
                }
                catch
                {
                    // One bad file must not break entire indexing
                }
            }

            if (changed)
                writer.Commit();
        });
    }

    /// <summary>
    /// Reads (path -> lastModifiedTicks) from the existing index.
    /// Used to skip unchanged files during indexing.
    /// </summary>
    private Dictionary<string, long> LoadExistingLastModifiedTicks(FSDirectory dir)
    {
        var map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (!DirectoryReader.IndexExists(dir))
                return map;

            using var reader = DirectoryReader.Open(dir);

            // Note: This reads stored fields for each doc.
            // It's okay for a first version and keeps the incremental logic simple.
            for (int docId = 0; docId < reader.MaxDoc; docId++)
            {
                var doc = reader.Document(docId);

                var path = doc.Get(FieldPath);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                long ticks = 0L;
                var ticksStr = doc.Get(FieldLastModifiedTicks);
                if (!string.IsNullOrWhiteSpace(ticksStr))
                    long.TryParse(ticksStr, out ticks);

                map[path] = ticks;
            }
        }
        catch
        {
            // If index is corrupt or unreadable, treat as "no metadata"
            // (so everything gets re-indexed)
        }

        return map;
    }
    
    private static string NormalizeForIndex(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        // Alt der ikke er bogstav eller ciffer => mellemrum
        s = System.Text.RegularExpressions.Regex.Replace(s, @"[^\p{L}\p{Nd}]+", " ");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();
        return s;
    }
}
