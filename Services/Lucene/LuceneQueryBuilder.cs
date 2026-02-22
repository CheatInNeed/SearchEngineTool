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
    private readonly LuceneVersion _version = LuceneVersion.LUCENE_48;

    public LuceneQueryBuilder(Analyzer analyzer)
    {
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
    }

    public Query Build(string rawQuery)
    {
        if (string.IsNullOrWhiteSpace(rawQuery))
            return new BooleanQuery();

        var normalized = NormalizeForQuery(NormalizeWhitespace(rawQuery));

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

        if (ordered.Count == 0)
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
        // F) MinimumShouldMatch to reduce junk results for long queries
        // ------------------------------------------------------------
        root.MinimumNumberShouldMatch = ComputeMinShouldMatch(tokens.Count);

        return root;
    }

    private static int ComputeMinShouldMatch(int tokenCount)
    {
        // Good default: require more matches as query gets longer
        if (tokenCount <= 2) return 1;
        if (tokenCount <= 4) return 2;
        if (tokenCount <= 7) return 3;
        return Math.Max(3, (int)Math.Round(tokenCount * 0.5)); // ~50%
    }

    private static PhraseQuery BuildPhrase(string field, List<string> orderedTerms, int slop)
    {
        var pq = new PhraseQuery { Slop = slop };
        foreach (var t in orderedTerms)
            pq.Add(new Term(field, t));
        return pq;
    }

    private List<string> AnalyzeToTerms(string fieldName, string text)
    {
        var terms = new List<string>();
        using var reader = new StringReader(text);
        using var tokenStream = _analyzer.GetTokenStream(fieldName, reader);

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