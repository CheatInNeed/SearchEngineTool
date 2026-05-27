using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
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
    private const string FieldContentInfix = "contentInfix";
    private const string FieldTitle = "title";
    private const string FieldLastModifiedTicksDv = "lastModifiedTicks_dv";
    private const string FieldFileSizeBytes = "fileSizeBytes";
    private const string FieldFileSizeBytesDv = "fileSizeBytes_dv";
    private const string FieldFolderDepth = "folderDepth";
    private const string FieldFolderDepthDv = "folderDepth_dv";
    private const string FieldContentHash = "contentHash";
    private const string FieldDocId = "docId";
    private const string FieldParentPath = "parentPath";
    private const string FieldChunkIndex = "chunkIndex";
    private const string FieldPageNumber = "pageNumber";

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
        _analyzer = new StandardAnalyzer(_version, CharArraySet.Empty);
    }

    public LuceneIndexService(LuceneIndexPaths indexPaths, Analyzer analyzer)
    {
        _indexPaths = indexPaths ?? throw new ArgumentNullException(nameof(indexPaths));
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
    }
    
    public Dictionary<string, long> GetIndexedLastModifiedTicks(string rootFolder)
    {
        var indexDirPath = _indexPaths.GetIndexDirectory(rootFolder);
        using var dir = FSDirectory.Open(new DirectoryInfo(indexDirPath));
        return LoadExistingLastModifiedTicks(dir);
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

    public bool HasPageNumberMetadata(string rootFolder)
    {
        try
        {
            var indexDirPath = _indexPaths.GetIndexDirectory(rootFolder);
            using var dir = FSDirectory.Open(new DirectoryInfo(indexDirPath));

            if (!DirectoryReader.IndexExists(dir))
                return false;

            using var reader = DirectoryReader.Open(dir);
            for (int docId = 0; docId < reader.MaxDoc; docId++)
            {
                var doc = reader.Document(docId);
                if (doc.GetField(FieldPageNumber) != null)
                    return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// Builds or updates the Lucene index for a given root folder.
    /// Incremental update: skips files where lastModifiedTicks is unchanged.
    /// </summary>
    public Task BuildOrUpdateIndexAsync(string rootFolder, IEnumerable<(string path, string content, int chunkIndex, int? pageNumber)> docs)
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

            foreach (var group in docs.GroupBy(d => d.path, StringComparer.OrdinalIgnoreCase))
            {
                var path = group.Key;

                long currentTicks = 0;
                try { currentTicks = File.GetLastWriteTimeUtc(path).Ticks; } catch { }

                if (existingTicksByPath.TryGetValue(path, out var oldTicks) && oldTicks == currentTicks)
                    continue;

                long fileSizeBytes = 0;
                try { fileSizeBytes = new FileInfo(path).Length; } catch { }

                int folderDepth = 0;
                try
                {
                    var rel = Path.GetRelativePath(rootFolder, path);
                    folderDepth = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .Count(x => !string.IsNullOrWhiteSpace(x)) - 1;
                    if (folderDepth < 0) folderDepth = 0;
                }
                catch { }

                var contentHash = "";
                try
                {
                    // Hash af hele filens chunks samlet (stabilt nok til nu)
                    var allText = string.Join("\n", group.OrderBy(x => x.chunkIndex).Select(x => x.content));
                    contentHash = ComputeSha1(allText);
                }
                catch { }
                
                // SLET gamle chunks for filen, ellers får du stale chunks hængende
                writer.DeleteDocuments(new Term(FieldPath, path));

                // Indeksér ALLE chunks for filen
                foreach (var (p, content, chunkIndex, pageNumber) in group.OrderBy(x => x.chunkIndex))
                {
                    if (string.IsNullOrWhiteSpace(p)) continue;
                    if (content == null) continue;

                    var fileName = Path.GetFileName(p);
                    var title = Path.GetFileNameWithoutExtension(p);
                    var ext = Path.GetExtension(p) ?? "";

                    var normalizedChunk = NormalizeForIndex(content);
                    var docId = $"{p}::{chunkIndex}";

                    var doc = new Document
                    {
                        new StringField(FieldPath, p, Field.Store.YES),
                        new StringField(FieldDocId, docId, Field.Store.YES),
                        new StringField(FieldParentPath, path, Field.Store.YES),
                        new StoredField(FieldChunkIndex, chunkIndex),
                        new StoredField(FieldPageNumber, pageNumber ?? 0),

                        new TextField(FieldTitle, title, Field.Store.YES),
                        new TextField(FieldFilename, fileName, Field.Store.YES),

                        new TextField(FieldContent, normalizedChunk, Field.Store.NO),
                        new TextField(FieldContentInfix, normalizedChunk, Field.Store.NO),

                        new StoredField(FieldContentStored, content),
                        new StringField(FieldExt, ext, Field.Store.YES),

                        new StoredField(FieldLastModifiedTicks, currentTicks),
                        
                        new StoredField(FieldFileSizeBytes, fileSizeBytes),
                        new StoredField(FieldFolderDepth, folderDepth),
                        new StringField(FieldContentHash, contentHash, Field.Store.YES),
                    };

                    writer.AddDocument(doc);
                }

                existingTicksByPath[path] = currentTicks;
                changed = true;
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
                
                var chunkField = doc.GetField(FieldChunkIndex);
                var chunkIndex = chunkField?.GetInt32Value() ?? -1;
                if (chunkIndex != 0)
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
    
    public void DeleteDocumentsMissingOnDisk(string rootFolder, HashSet<string> existingFiles)
    {
        var indexDirPath = _indexPaths.GetIndexDirectory(rootFolder);
        using var dir = FSDirectory.Open(new DirectoryInfo(indexDirPath));

        if (!DirectoryReader.IndexExists(dir))
            return;

        var config = new IndexWriterConfig(_version, _analyzer)
        {
            OpenMode = OpenMode.CREATE_OR_APPEND
        };

        using var writer = new IndexWriter(dir, config);
        using var reader = DirectoryReader.Open(dir);

        for (int docId = 0; docId < reader.MaxDoc; docId++)
        {
            var doc = reader.Document(docId);
            var path = doc.Get(FieldPath);
            if (string.IsNullOrWhiteSpace(path)) continue;

            if (!existingFiles.Contains(path))
            {
                writer.DeleteDocuments(new Term(FieldPath, path));
            }
        }

        writer.Commit();
    }
    
    private static string ComputeSha1(string s)
    {
        try
        {
            using var sha1 = System.Security.Cryptography.SHA1.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(s ?? "");
            var hash = sha1.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }
        catch
        {
            return "";
        }
    }


}
