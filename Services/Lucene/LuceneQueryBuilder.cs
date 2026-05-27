using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Lucene.Net.Analysis;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Util;
using Lucene.Net.Analysis.TokenAttributes;

namespace FileSearchTool.Services.Lucene;

public class LuceneQueryBuilder
{
    private const string FieldTitle = "title";
    private const string FieldFilename = "filename";
    private const string FieldContent = "content";
    private const string FieldContentInfix = "contentInfix";

    private readonly Analyzer _analyzer;
    private readonly Analyzer _strictPhraseAnalyzer;

    public LuceneQueryBuilder(Analyzer analyzer)
    {
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
        _strictPhraseAnalyzer = AnalyzerFactory.Create(LuceneVersion.LUCENE_48, enableSynonyms: false);
    }

    public Query Build(string rawQuery, IndexReader? reader = null)
    {
        if (string.IsNullOrWhiteSpace(rawQuery))
            return new BooleanQuery();

        var quotedPhrases = ExtractQuotedPhrases(rawQuery)
            .Select(q => AnalyzeToTerms(_strictPhraseAnalyzer, FieldContent, NormalizeForQuery(NormalizeWhitespace(q))))
            .Where(q => q.Count >= 2)
            .ToList();

        var unquotedRaw = RemoveQuotedPhrases(rawQuery);
        var normalized = NormalizeForQuery(NormalizeWhitespace(unquotedRaw));

        // ===== Field weighting =====
        const float BoostTitle = 4.0f;
        const float BoostFilename = 2.5f;
        const float BoostContent = 1.0f;

        // ===== “Golden query” boosts =====
        const float ExactPhraseBoost = 10.0f;   // requirement
        const float ProxPhraseBoost  = 5.0f;    // requirement
        const float FuzzyBoost       = 1.0f;    // requirement
        const float BroadBoost       = 0.8f;
        const float InfixBoost       = 0.6f;

        const int ExactSlop = 0;
        const int ProxSlop  = 10;

        // Analyze query (same analyzer as index)
        var ordered = AnalyzeToTerms(FieldContent, normalized)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        if (ordered.Count == 0 && quotedPhrases.Count == 0)
            return new BooleanQuery();

        // Distinct tokens for “bag-of-words” parts
        var tokens = ordered
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Root query: SHOULD-layers (answer finding)
        var root = new BooleanQuery();

        void AddShould(Query q, float boost)
        {
            q.Boost = boost;
            root.Add(q, Occur.SHOULD);
        }

        void AddMust(Query q, float boost)
        {
            q.Boost = boost;
            root.Add(q, Occur.MUST);
        }

        // Quoted text is strict: each quoted phrase must occur in title, filename, or content.
        foreach (var phraseTerms in quotedPhrases)
        {
            var quoted = new BooleanQuery { MinimumNumberShouldMatch = 1 };
            quoted.Add(Boost(BuildPhrase(FieldTitle, phraseTerms, ExactSlop), ExactPhraseBoost * BoostTitle), Occur.SHOULD);
            quoted.Add(Boost(BuildPhrase(FieldFilename, phraseTerms, ExactSlop), ExactPhraseBoost * BoostFilename), Occur.SHOULD);
            quoted.Add(Boost(BuildPhrase(FieldContent, phraseTerms, ExactSlop), ExactPhraseBoost * BoostContent), Occur.SHOULD);
            AddMust(quoted, 1.0f);
        }

        // ------------------------------------------------------------
        // A) Exact phrase (slop=0) with HIGH boost across fields
        // ------------------------------------------------------------
        if (ordered.Count >= 2)
        {
            AddShould(BuildPhrase(FieldTitle, ordered, ExactSlop), ExactPhraseBoost * BoostTitle);
            AddShould(BuildPhrase(FieldFilename, ordered, ExactSlop), ExactPhraseBoost * BoostFilename);
            AddShould(BuildPhrase(FieldContent, ordered, ExactSlop), ExactPhraseBoost * BoostContent);
        }

        // ------------------------------------------------------------
        // B) Proximity phrase (slop=10) with MEDIUM boost
        // ------------------------------------------------------------
        if (ordered.Count >= 2)
        {
            AddShould(BuildPhrase(FieldTitle, ordered, ProxSlop), ProxPhraseBoost * BoostTitle);
            AddShould(BuildPhrase(FieldFilename, ordered, ProxSlop), ProxPhraseBoost * BoostFilename);
            AddShould(BuildPhrase(FieldContent, ordered, ProxSlop), ProxPhraseBoost * BoostContent);
        }

        // ------------------------------------------------------------
        // C) Broad match: TermQuery (+ PrefixQuery for longer tokens)
        // ------------------------------------------------------------
        foreach (var t in tokens)
        {
            // Title / filename / content terms
            AddShould(new TermQuery(new Term(FieldTitle, t)), BroadBoost * BoostTitle);
            AddShould(new TermQuery(new Term(FieldFilename, t)), BroadBoost * BoostFilename);
            AddShould(new TermQuery(new Term(FieldContent, t)), BroadBoost * BoostContent);

            // PrefixQuery is great for “starts-with” on long-ish tokens
            if (t.Length >= 4)
            {
                AddShould(new PrefixQuery(new Term(FieldTitle, t)), 0.5f * BroadBoost * BoostTitle);
                AddShould(new PrefixQuery(new Term(FieldContent, t)), 0.4f * BroadBoost * BoostContent);
            }

            // Infix ngram field: query with TermQuery on raw token (your analyzer already makes ngrams at index-time)
            if (t.Length >= 3)
                AddShould(new TermQuery(new Term(FieldContentInfix, t)), InfixBoost);
        }

        // ------------------------------------------------------------
        // D) Fuzzy term match (typo tolerance), LOW boost
        // ------------------------------------------------------------
        foreach (var t in tokens)
        {
            // Avoid fuzzifying very short tokens
            if (t.Length < 4) continue;

            // maxEdits: 1 or 2 — use 1 for performance, bump to 2 if you need it
            var fqTitle = new FuzzyQuery(new Term(FieldTitle, t), maxEdits: 1);
            var fqContent = new FuzzyQuery(new Term(FieldContent, t), maxEdits: 1);

            AddShould(fqTitle, FuzzyBoost * BoostTitle);
            AddShould(fqContent, FuzzyBoost * BoostContent);
        }

        // ------------------------------------------------------------
        // E) “All terms” constraint as a SOFT signal (SHOULD)
        // (This helps exam queries a lot)
        // ------------------------------------------------------------
        if (tokens.Count >= 2)
        {
            var all = new BooleanQuery();
            foreach (var t in tokens)
            {
                // Put MUST on content only (title MAY miss some terms)
                all.Add(new TermQuery(new Term(FieldContent, t)), Occur.MUST);
            }
            AddShould(all, 3.0f); // soft bonus
        }

        // ------------------------------------------------------------
        // F) Long pasted exam questions: boost distinctive/high-IDF terms.
        // ------------------------------------------------------------
        foreach (var rare in SelectDistinctiveTerms(tokens, reader).Take(8))
        {
            AddShould(new TermQuery(new Term(FieldContent, rare.Term)), rare.Boost);
            AddShould(new TermQuery(new Term(FieldTitle, rare.Term)), rare.Boost * BoostTitle);
        }

        // ------------------------------------------------------------
        // G) MinimumShouldMatch to reduce junk results for long queries
        // ------------------------------------------------------------
        root.MinimumNumberShouldMatch = ComputeMinShouldMatch(tokens.Count, quotedPhrases.Count);

        return root;
    }
    
    public List<string> AnalyzeQueryTerms(string rawQuery)
    {
        if (string.IsNullOrWhiteSpace(rawQuery)) return new List<string>();

        var normalized = NormalizeForQuery(NormalizeWhitespace(rawQuery));

        // Brug content-field så vi rammer samme pipeline som retrieval
        return AnalyzeToTerms(_analyzer, FieldContent, normalized)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int ComputeMinShouldMatch(int tokenCount, int quotedPhraseCount)
    {
        if (tokenCount == 0 || quotedPhraseCount > 0 && tokenCount <= 2)
            return 0;

        if (tokenCount <= 2) return 1;
        if (tokenCount <= 4) return 2;
        if (tokenCount <= 8) return 3;
        if (tokenCount <= 16) return Math.Max(4, (int)Math.Ceiling(tokenCount * 0.45));
        if (tokenCount <= 32) return Math.Max(5, (int)Math.Ceiling(tokenCount * 0.35));
        return Math.Max(8, (int)Math.Ceiling(tokenCount * 0.25));
    }

    private static PhraseQuery BuildPhrase(string field, List<string> orderedTerms, int slop)
    {
        var pq = new PhraseQuery { Slop = slop };
        foreach (var t in orderedTerms)
            pq.Add(new Term(field, t));
        return pq;
    }

    private static Query Boost(Query q, float boost)
    {
        q.Boost = boost;
        return q;
    }

    private IEnumerable<(string Term, float Boost)> SelectDistinctiveTerms(IReadOnlyList<string> tokens, IndexReader? reader)
    {
        if (tokens.Count < 8)
            return Array.Empty<(string Term, float Boost)>();

        var distinct = tokens
            .Distinct(StringComparer.Ordinal)
            .Where(t => t.Length >= 5)
            .ToList();

        var maxDoc = Math.Max(1, reader?.NumDocs ?? 1);

        return distinct
            .Select(t =>
            {
                var df = 0;
                try
                {
                    df = reader?.DocFreq(new Term(FieldContent, t)) ?? 0;
                }
                catch
                {
                    df = 0;
                }

                var idf = MathF.Log(1f + (maxDoc / (float)(df + 1)));
                var lengthBoost = MathF.Min(1.5f, t.Length / 10f);
                return (Term: t, Boost: 2.5f + idf + lengthBoost);
            })
            .OrderByDescending(x => x.Boost);
    }

    private static List<string> ExtractQuotedPhrases(string rawQuery)
    {
        return Regex.Matches(rawQuery, "\"([^\"]+)\"")
            .Cast<Match>()
            .Select(m => m.Groups[1].Value.Trim())
            .Where(x => x.Length > 0)
            .ToList();
    }

    private static string RemoveQuotedPhrases(string rawQuery)
    {
        return Regex.Replace(rawQuery, "\"([^\"]+)\"", " ");
    }

    private List<string> AnalyzeToTerms(string fieldName, string text)
    {
        return AnalyzeToTerms(_analyzer, fieldName, text);
    }

    private static List<string> AnalyzeToTerms(Analyzer analyzer, string fieldName, string text)
    {
        var terms = new List<string>();
        using var reader = new StringReader(text);
        using var tokenStream = analyzer.GetTokenStream(fieldName, reader);

        var termAttr = tokenStream.AddAttribute<ICharTermAttribute>();
        tokenStream.Reset();

        while (tokenStream.IncrementToken())
        {
            var term = termAttr.ToString();
            if (!string.IsNullOrWhiteSpace(term))
                terms.Add(term);
        }

        tokenStream.End();
        return terms;
    }

    private static string NormalizeWhitespace(string s)
    {
        s = s.Trim();
        s = Regex.Replace(s, @"\s+", " ");
        return s;
    }

    private static string NormalizeForQuery(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = Regex.Replace(s, @"[^\p{L}\p{Nd}]+", " ");
        s = Regex.Replace(s, @"\s+", " ").Trim();
        return s;
    }
}
