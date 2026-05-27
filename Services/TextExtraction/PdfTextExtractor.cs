using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using UglyToad.PdfPig;

namespace FileSearchTool.Services.TextExtraction;

public class PdfTextExtractor: ITextExtractor
{
    public string ExtractText(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) throw new ArgumentException();
        if (!File.Exists(filePath)) throw new FileNotFoundException();
        if (!string.Equals(Path.GetExtension(filePath), ".pdf", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException();

        try
        {
            using var doc = PdfDocument.Open((filePath));
            var sb = new StringBuilder();

            foreach (var page in doc.GetPages())
            {
                sb.AppendLine(ExtractPageText(page));
            }

            var result = sb.ToString();

            result = result.Replace("\r", " ");
            result = result.Replace("\n", " ");
            while (result.Contains("  "))
            {
                result = result.Replace("  ", " ");
            }

            return result;
        }
        catch (Exception e)
        {
            throw new Exception($"Failed to extract text from PDF: {filePath}", e);
        }
    }

    public IReadOnlyList<(int PageNumber, string Text)> ExtractPages(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) throw new ArgumentException();
        if (!File.Exists(filePath)) throw new FileNotFoundException();
        if (!string.Equals(Path.GetExtension(filePath), ".pdf", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException();

        try
        {
            using var doc = PdfDocument.Open(filePath);
            var pages = new List<(int PageNumber, string Text)>();

            foreach (var page in doc.GetPages())
            {
                var text = NormalizeWhitespace(ExtractPageText(page));
                if (!string.IsNullOrWhiteSpace(text))
                    pages.Add((page.Number, text));
            }

            return pages;
        }
        catch (Exception e)
        {
            throw new Exception($"Failed to extract PDF pages: {filePath}", e);
        }
    }

    private static string ExtractPageText(UglyToad.PdfPig.Content.Page page)
    {
        var sb = new StringBuilder();

        foreach (var word in page.GetWords())
        {
            sb.Append(word.Text);
            sb.Append(' ');
        }

        return sb.ToString();
    }

    private static string NormalizeWhitespace(string text)
    {
        var result = text.Replace("\r", " ").Replace("\n", " ");
        while (result.Contains("  "))
            result = result.Replace("  ", " ");
        return result.Trim();
    }
}
