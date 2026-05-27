using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FileSearchTool.Services;

public class FileCrawler
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt",
            ".md",
            ".markdown",
            ".pdf",
            ".docx"
        };

    public IEnumerable<string> GetFiles(string rootFolder)
    {
        if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
            return Enumerable.Empty<string>();

        return Directory.EnumerateFiles(rootFolder, "*.*", SearchOption.AllDirectories)
            .Where(path => AllowedExtensions.Contains(Path.GetExtension(path)));
    }
}
