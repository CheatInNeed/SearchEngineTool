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

    public static List<PdfHit> FindAllHits(string pdfPath, string rawQuery, IEnumerable<string>? extraTokens = null)
    {
        var hits = new List<PdfHit>();
        if (string.IsNullOrWhiteSpace(pdfPath) || string.IsNullOrWhiteSpace(rawQuery))
            return hits;

        var qNorm = Norm(rawQuery);
        var qTokens = qNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        
        if (extraTokens != null)
        {
            qTokens.AddRange(extraTokens
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => Norm(x)));
            qTokens = qTokens.Distinct(StringComparer.Ordinal).ToList();
        }
        
        var hasPhrase = qTokens.Count >= 2;
        
        // Sub-phrases fra query (bigrams/trigrams) – i rækkefølge
        var qBigrams = new List<string>();
        var qTrigrams = new List<string>();

        for (int i = 0; i <= qTokens.Count - 2; i++)
            qBigrams.Add($"{qTokens[i]} {qTokens[i + 1]}");

        for (int i = 0; i <= qTokens.Count - 3; i++)
            qTrigrams.Add($"{qTokens[i]} {qTokens[i + 1]} {qTokens[i + 2]}");
        
        qBigrams = qBigrams.Distinct(StringComparer.Ordinal).ToList();
        qTrigrams = qTrigrams.Distinct(StringComparer.Ordinal).ToList();

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
            
            // A0) Trigram hits (højere end phrase og token)
            if (qTrigrams.Count > 0)
            {
                for (int i = 0; i <= flat.Count - 3; i++)
                {
                    var tri = $"{flat[i].token} {flat[i + 1].token} {flat[i + 2].token}";
                    if (!qTrigrams.Contains(tri, StringComparer.Ordinal)) continue;

                    var wordsTri = new[] { flat[i].word, flat[i + 1].word, flat[i + 2].word }
                        .Distinct()
                        .ToList();

                    var rects = wordsTri.Select(w => ToAvaloniaRect(w.BoundingBox)).ToArray();

                    var firstWord = flat[i].word;
                    var startIdx = words.IndexOf(firstWord);

                    var ctx = startIdx >= 0
                        ? BuildMicroContext(originalWordTexts, startIdx, Math.Max(1, wordsTri.Count))
                        : $"… {tri} …";

                    var penalty = ContextPenalty(ctx);

                    hits.Add(new PdfHit(
                        ++hitIndex,
                        page.Number,
                        rects,
                        Score: (6.0f + EarlyBonus(page.Number, i)) - penalty,
                        MatchedToken: tri,
                        Context: ctx));
                }
            }
            
            if (qBigrams.Count > 0)
            {
                for (int i = 0; i <= flat.Count - 2; i++)
                {
                    var bi = $"{flat[i].token} {flat[i + 1].token}";
                    if (!qBigrams.Contains(bi, StringComparer.Ordinal)) continue;

                    var wordsBi = new[] { flat[i].word, flat[i + 1].word }
                        .Distinct()
                        .ToList();

                    var rects = wordsBi.Select(w => ToAvaloniaRect(w.BoundingBox)).ToArray();

                    var firstWord = flat[i].word;
                    var startIdx = words.IndexOf(firstWord);

                    var ctx = startIdx >= 0
                        ? BuildMicroContext(originalWordTexts, startIdx, Math.Max(1, wordsBi.Count))
                        : $"… {bi} …";

                    var penalty = ContextPenalty(ctx);

                    hits.Add(new PdfHit(
                        ++hitIndex,
                        page.Number,
                        rects,
                        Score: (4.0f + EarlyBonus(page.Number, i)) - penalty,
                        MatchedToken: bi,
                        Context: ctx));
                }
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
                            Score: 6.0f + EarlyBonus(page.Number, i) + PhraseLengthBonus(qTokens.Count),
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
                            Score: 2.0f + EarlyBonus(page.Number, startIdx) + TokenRarityBonus(q),
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
        hits = CollapseOverlappingHits(hits);
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

    private static float EarlyBonus(int pageNumber, int wordIndexOnPage)
    {
        // Tidlige sider prioriteres hårdere end senere sider
        // Side 1 får +1.0, side 2 +0.6, side 3 +0.4, ...
        var pageBoost = 1.0f / (1.0f + (pageNumber - 1) * 0.7f);

        // Tidlige ord på siden boostes let
        // wordIndexOnPage 0 => +0.5, 50 => ~0.1
        var posBoost = 0.5f / (1.0f + wordIndexOnPage / 20.0f);

        return pageBoost + posBoost;
    }

    private static float PhraseLengthBonus(int tokenCount)
    {
        // længere fraser er mere specifikke => højere score
        // 2 tokens => +0.2, 3 => +0.4, 4 => +0.6 ...
        return 0.2f * Math.Max(0, tokenCount - 1);
    }

    private static float TokenRarityBonus(string token)
    {
        // Meget simpel rarity proxy uden at hente IDF:
        // Lange tokens er ofte mere specifikke, korte er mere støj
        if (string.IsNullOrWhiteSpace(token)) return 0f;
        token = token.Trim();
        if (token.Length >= 10) return 0.6f;
        if (token.Length >= 6) return 0.3f;
        return 0.0f;
    }

    private static float ContextPenalty(string ctx)
    {
        if (string.IsNullOrWhiteSpace(ctx)) return 0f;

        var s = ctx.ToLowerInvariant();

        // Lille "INDEX/TOC" penalty – kun til tie-break
        float p = 0f;
        if (s.Contains("index", StringComparison.Ordinal) || s.Contains("table of contents", StringComparison.Ordinal))
            p += 0.6f;

        // Høj digit-ratio (ofte index/tabeller) => lille penalty
        int digits = 0, letters = 0;
        foreach (var ch in s)
        {
            if (char.IsDigit(ch)) digits++;
            else if (char.IsLetter(ch)) letters++;
        }

        if (letters > 0)
        {
            var ratio = digits / (float)(letters + digits);
            if (ratio > 0.35f) p += 0.4f;
        }

        return p;
    }
    
    private static List<PdfHit> CollapseOverlappingHits(List<PdfHit> hits)
{
    if (hits.Count <= 1) return hits;

    // Sortér: bedste først (høj score), så længere match (flere ord), så lavere page
    var ordered = hits
        .OrderByDescending(h => h.Score)
        .ThenByDescending(h => TokenCount(h.MatchedToken))
        .ThenBy(h => h.PageNumber)
        .ToList();

    var kept = new List<PdfHit>();

    foreach (var h in ordered)
    {
        bool overlaps = false;

        foreach (var k in kept)
        {
            if (h.PageNumber != k.PageNumber) continue;

            // Hvis hit-bounds overlapper meget, så er det samme “sted” i teksten
            var a = Bounds(h);
            var b = Bounds(k);

            if (OverlapRatio(a, b) >= 0.45) // tune 0.35–0.60
            {
                overlaps = true;
                break;
            }
        }

        if (!overlaps)
            kept.Add(h);
    }

    // Bevar original HitIndex rækkefølge-indekser er ikke vigtig her,
    // PdfPreviewViewModel sorterer alligevel på Score.
    return kept;
}

private static int TokenCount(string s)
{
    if (string.IsNullOrWhiteSpace(s)) return 0;
    return s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
}

private static Rect Bounds(PdfHit h)
{
    if (h.Boxes == null || h.Boxes.Length == 0)
        return new Rect(0, 0, 0, 0);

    double left = h.Boxes[0].X;
    double top = h.Boxes[0].Y;
    double right = h.Boxes[0].X + h.Boxes[0].Width;
    double bottom = h.Boxes[0].Y + h.Boxes[0].Height;

    foreach (var r in h.Boxes)
    {
        left = Math.Min(left, r.X);
        top = Math.Min(top, r.Y);
        right = Math.Max(right, r.X + r.Width);
        bottom = Math.Max(bottom, r.Y + r.Height);
    }

    return new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
}

// overlap ratio = intersection area / min(areaA, areaB)
// => “hvor meget dækker de samme område”
private static double OverlapRatio(Rect a, Rect b)
{
    var ix1 = Math.Max(a.X, b.X);
    var iy1 = Math.Max(a.Y, b.Y);
    var ix2 = Math.Min(a.X + a.Width, b.X + b.Width);
    var iy2 = Math.Min(a.Y + a.Height, b.Y + b.Height);

    var iw = Math.Max(0, ix2 - ix1);
    var ih = Math.Max(0, iy2 - iy1);
    var inter = iw * ih;

    var areaA = a.Width * a.Height;
    var areaB = b.Width * b.Height;

    if (areaA <= 0 || areaB <= 0) return 0;

    return inter / Math.Min(areaA, areaB);
}
}
