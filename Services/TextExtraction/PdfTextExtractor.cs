using System;
using System.IO;
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
                var text = page.Text;
                sb.AppendLine(text);
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
}