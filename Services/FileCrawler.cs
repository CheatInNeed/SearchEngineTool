using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FileSearchTool.Services;

public class FileCrawler
{
    private static readonly string[] AllowedExtensions = { ".txt", ".md", ".cs" };

    public IEnumerable<string> GetFiles(string rootFolder)
    {
        if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
            return Enumerable.Empty<string>();

        return Directory.EnumerateFiles(rootFolder, "*.*", SearchOption.AllDirectories)
            .Where(path => AllowedExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()));
    }
}