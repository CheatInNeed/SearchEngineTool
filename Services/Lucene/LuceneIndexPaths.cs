using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FileSearchTool.Services.Lucene;

public class LuceneIndexPaths
{
    /**
     * Returns the base folder of the app-data. 
     */
    private static string GetAppDataBase()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appDir = Path.Combine(baseDir, "FileSearchTool");

        // Sørg for mappen findes
        Directory.CreateDirectory(appDir);

        return appDir;
    }
    
    /**
     * Folder where all the Lucene index-folders will be stored.
     */
    private static string GetLuceneRoot()
    {
        var luceneDir = Path.Combine(GetAppDataBase(), "lucene-index");

        Directory.CreateDirectory(luceneDir);

        return luceneDir;
    }
    /**
     * Creates and returns an index path for selected folder. Each folder will have its own Lucene index.
     */
    public string GetIndexDirectory(string rootFolder)
    {
        if (string.IsNullOrWhiteSpace(rootFolder))
            throw new ArgumentException(nameof(rootFolder));

        var folderHash = ComputeStableHash(rootFolder);

        var indexPath = Path.Combine(GetLuceneRoot(), folderHash);

        Directory.CreateDirectory(indexPath);

        return indexPath;
    }
    
    private static string ComputeStableHash(string input)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = sha.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes);
    }
}