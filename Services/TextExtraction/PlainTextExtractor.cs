using System;
using System.IO;

namespace FileSearchTool.Services.TextExtraction;

public class PlainTextExtractor : ITextExtractor
{
    public string ExtractText(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException();
        if (!File.Exists(filePath)) throw new FileNotFoundException();

        return File.ReadAllText(filePath);
    }
}