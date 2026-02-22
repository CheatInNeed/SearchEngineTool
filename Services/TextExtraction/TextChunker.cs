using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FileSearchTool.Services.TextExtraction;

public static class TextChunker
{
    // Split on blank lines or strong punctuation boundaries, then pack into ~target chars
    public static List<string> ToChunks(string text, int targetChars = 1200, int overlapChars = 150)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        // Normalize whitespace a bit
        text = text.Replace("\r", "\n");
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");

        // Paragraph-like units
        var parts = text
            .Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

        // If PDF extraction produced single “mega paragraph”, fallback to sentence-ish split
        if (parts.Count <= 1 && text.Length > targetChars * 2)
        {
            parts = Regex.Split(text, @"(?<=[\.\!\?])\s+")
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();
        }

        var chunks = new List<string>();
        var current = "";

        foreach (var p in parts)
        {
            if (current.Length == 0)
            {
                current = p;
                continue;
            }

            if (current.Length + 1 + p.Length <= targetChars)
            {
                current += "\n" + p;
            }
            else
            {
                chunks.Add(current);

                // overlap tail
                if (overlapChars > 0 && current.Length > overlapChars)
                    current = current.Substring(current.Length - overlapChars) + "\n" + p;
                else
                    current = p;
            }
        }

        if (current.Length > 0)
            chunks.Add(current);

        return chunks;
    }
}