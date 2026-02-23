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

                var words = page.GetWords();

                foreach (var word in words)
                {
                    sb.Append(word.Text);
                    sb.Append(" ");
                }

                sb.AppendLine();
            }
            

            var result = sb.ToString();

            result = NormalizePdfText(result);

            result = result.Replace("\r", " ").Replace("\n", " ");
            while (result.Contains("  "))
                result = result.Replace("  ", " ");

            return result;
        }
        catch (Exception e)
        {
            throw new Exception($"Failed to extract text from PDF: {filePath}", e);
        }
    }
    
    private static string NormalizePdfText(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        // Remove soft hyphen + zero-width chars
        s = s.Replace("\u00AD", " "); // soft hyphen
        s = s.Replace("\u200B", " "); // zero width space
        s = s.Replace("\uFEFF", " "); // BOM / zero width no-break space

        // Normalize various dashes to space (so "hash-based" => "hash based")
        var dashes = new[]
        {
            '\u2010', // hyphen
            '\u2011', // non-breaking hyphen
            '\u2012', // figure dash
            '\u2013', // en dash
            '\u2014', // em dash
            '\u2212'  // minus sign
        };

        foreach (var ch in dashes)
            s = s.Replace(ch, ' ');

        return s;
    }
}