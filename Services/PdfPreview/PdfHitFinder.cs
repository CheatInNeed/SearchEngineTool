using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using FileSearchTool.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace FileSearchTool.Services.PdfPreview;

public static class PdfHitFinder
{
    // Samme normalisering-ide som jeres søgning (kun bogstav/ciffer)
    private static string Norm(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.ToLowerInvariant();
        s = Regex.Replace(s, @"[^\p{L}\p{Nd}]+", " ");
        s = Regex.Replace(s, @"\s+", " ").Trim();
        return s;
    }

    public static List<PdfHit> FindAllHits(string pdfPath, string rawQuery)
    {
        var hits = new List<PdfHit>();
        if (string.IsNullOrWhiteSpace(pdfPath) || string.IsNullOrWhiteSpace(rawQuery))
            return hits;

        var qNorm = Norm(rawQuery);
        var qTokens = qNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var hasPhrase = qTokens.Count >= 2;

        using var doc = PdfDocument.Open(pdfPath);

        int hitIndex = 0;

        foreach (var page in doc.GetPages())
        {
            // PdfPig words + bbox
            var words = page.GetWords().ToList();
            var originalWordTexts = words.Select(w => w.Text).ToList();
            if (words.Count == 0) continue;

            // normaliser hvert ord til token(s)
            // (nogle "words" kan indeholde tegn; vi splitter efter Norm)
            var flat = new List<(string token, Word word)>();
            foreach (var w in words)
            {
                var wn = Norm(w.Text);
                if (string.IsNullOrWhiteSpace(wn)) continue;
                foreach (var t in wn.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    flat.Add((t, w));
            }

            // A) Phrase hits (sekvens)
            if (hasPhrase)
            {
                for (int i = 0; i <= flat.Count - qTokens.Count; i++)
                {
                    bool ok = true;
                    for (int k = 0; k < qTokens.Count; k++)
                    {
                        if (!string.Equals(flat[i + k].token, qTokens[k], StringComparison.Ordinal))
                        {
                            ok = false;
                            break;
                        }
                    }

                    if (ok)
                    {
                        var phraseWords = flat.Skip(i).Take(qTokens.Count).Select(x => x.word).Distinct().ToList();
                        var rects = phraseWords.Select(w => ToAvaloniaRect(w.BoundingBox)).ToArray();
                        
                        var firstWord = flat[i].word;
                        var startIdx = words.IndexOf(firstWord);
                        var ctx = startIdx >= 0
                            ? BuildMicroContext(originalWordTexts, startIdx, Math.Max(1, phraseWords.Count))
                            : $"… {string.Join(" ", qTokens)} …";

                        hits.Add(new PdfHit(
                            ++hitIndex,
                            page.Number,
                            rects,
                            Score: 3.0f,
                            MatchedToken: string.Join(" ", qTokens),
                            Context: ctx));


                    }
                }
            }

            // B) Token hits (enkeltord) med exact + infix
            var distinctQueryTokens = qTokens.Distinct(StringComparer.Ordinal).ToList();

            foreach (var (token, word) in flat)
            {
                foreach (var q in distinctQueryTokens)
                {
                    if (q.Length == 0) continue;
                    
                    var startIdx = words.IndexOf(word);
                    var ctx = startIdx >= 0
                        ? BuildMicroContext(originalWordTexts, startIdx, 1)
                        : $"… {word.Text} …";
                    
                    // Exact match: høj score
                    if (string.Equals(token, q, StringComparison.Ordinal))
                    {
                        
                        hits.Add(new PdfHit(++hitIndex, page.Number,
                            new[] { ToAvaloniaRect(word.BoundingBox) },
                            Score: 2.0f,
                            MatchedToken: q,
                            Context: ctx));
                        continue;
                    }

                    // Infix match: token indeholder query (fx bitcoin indeholder itcoi)
                    if (q.Length >= 3 && token.Contains(q, StringComparison.Ordinal))
                    {
                        hits.Add(new PdfHit(++hitIndex, page.Number,
                            new[] { ToAvaloniaRect(word.BoundingBox) },
                            Score: 1.0f,
                            MatchedToken: q,
                            Context: ctx));
                    }
                }
            }

        }

        return hits;
    }

    private static Rect ToAvaloniaRect(PdfRectangle r)
    {
        // PdfPig: origin typisk bottom-left (PDF space)
        // Vi gemmer raw PDF coords og konverterer senere når vi tegner overlay.
        return new Rect(r.Left, r.Bottom, r.Width, r.Height);
    }
    
    private static string BuildMicroContext(List<string> originalWords, int startWordIndex, int lengthWords)
    {
        // 3 ord på hver side
        int left = Math.Max(0, startWordIndex - 3);
        int right = Math.Min(originalWords.Count - 1, (startWordIndex + lengthWords - 1) + 3);

        var slice = originalWords.Skip(left).Take(right - left + 1).ToList();

        // Markér selve match-blokken i midten med [ ]
        int markStart = startWordIndex - left;
        int markEnd = markStart + lengthWords - 1;

        for (int i = 0; i < slice.Count; i++)
        {
            if (i == markStart) slice[i] = "[" + slice[i];
            if (i == markEnd) slice[i] = slice[i] + "]";
        }

        var s = string.Join(" ", slice);
        if (left > 0) s = "… " + s;
        if (right < originalWords.Count - 1) s = s + " …";
        return s;
    }

    
}
