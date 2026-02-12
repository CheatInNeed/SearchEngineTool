using System;
using System.IO;
using System.Text;

namespace FileSearchTool.Services.TextExtraction;

public static class TextExtractorFactory
{
    public static ITextExtractor Create(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) throw new ArgumentException();

        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        switch (extension)
        {
            case ".pdf":
                return new PdfTextExtractor();
            default:
                throw new NotSupportedException($"Filetype not supported: {extension}");
        }
    }
}