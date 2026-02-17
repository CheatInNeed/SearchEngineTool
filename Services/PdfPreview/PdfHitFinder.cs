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

                        hits.Add(new PdfHit(++hitIndex, page.Number, rects, Score: 3.0f, MatchedToken: string.Join(" ", qTokens)));


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

                    // Exact match: høj score
                    if (string.Equals(token, q, StringComparison.Ordinal))
                    {
                        hits.Add(new PdfHit(++hitIndex, page.Number,
                            new[] { ToAvaloniaRect(word.BoundingBox) },
                            Score: 2.0f,
                            MatchedToken: q));
                        continue;
                    }

                    // Infix match: token indeholder query (fx bitcoin indeholder itcoi)
                    if (q.Length >= 3 && token.Contains(q, StringComparison.Ordinal))
                    {
                        hits.Add(new PdfHit(++hitIndex, page.Number,
                            new[] { ToAvaloniaRect(word.BoundingBox) },
                            Score: 1.0f,
                            MatchedToken: q));
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
}
