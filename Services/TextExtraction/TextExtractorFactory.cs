using System;
using System.IO;
using System.Text;

namespace FileSearchTool.Services.TextExtraction;

public static class TextExtractorFactory
{
    public static ITextExtractor Create(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException();

        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".pdf" => new PdfTextExtractor(),
            ".txt" => new PlainTextExtractor(),
            ".md" => new PlainTextExtractor(),
            ".markdown" => new PlainTextExtractor(),
            ".docx" => new DocxTextExtractor(),
            _ => throw new NotSupportedException()

        };
    }
}
