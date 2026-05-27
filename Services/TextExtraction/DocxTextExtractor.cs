namespace FileSearchTool.Services.TextExtraction;

using System;
using System.IO;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

public class DocxTextExtractor : ITextExtractor
{
    public string ExtractText(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException();
        if (!File.Exists(filePath)) throw new FileNotFoundException();
        if (!string.Equals(Path.GetExtension(filePath), ".docx", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException();

        try
        {
            using var document = WordprocessingDocument.Open(filePath, false);
            var body = document.MainDocumentPart?.Document?.Body;
            if (body == null) return "";

            var sb = new StringBuilder();

            foreach (var paragraph in body.Descendants<Paragraph>())
            {
                var parts = paragraph
                    .Descendants()
                    .Select(e => e switch
                    {
                        Text text => text.Text,
                        TabChar => "\t",
                        Break => "\n",
                        _ => null
                    })
                    .Where(x => x != null);

                var line = string.Concat(parts);
                if (!string.IsNullOrWhiteSpace(line))
                    sb.AppendLine(line);
            }

            return sb.ToString();
        }
        catch (Exception e)
        {
            throw new Exception($"Failed to extract text from DOCX: {filePath}", e);
        }
    }
}
