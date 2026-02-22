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

    /// <summary>
    /// Builds a Lucene query from a raw "free text" input:
    /// - Boost phrase query higher than token queries
    /// - Token queries are OR/SHOULD
    /// - Adds a small minimum-should-match for longer queries
    /// </summary>
    public Query Build(string rawQuery)
{
    if (string.IsNullOrWhiteSpace(rawQuery))
        return new BooleanQuery();

    var normalized = NormalizeWhitespace(rawQuery);
    normalized = NormalizeForQuery(normalized);

    // === Tunable boosts (Stage 1: retrieval only) ===
    const float BoostTitle = 4.0f;
    const float BoostFilename = 2.5f;
    const float BoostContent = 1.0f;

    const int ExactPhraseSlop = 3;   // 2–3
    const int LoosePhraseSlop = 12;  // 10–15

    const float ExactPhraseBoost = 12.0f;
    const float LoosePhraseBoost = 6.0f;

    const float AllTermsBoost = 5.0f;
    const float TokenBoost = 1.2f;
    const float InfixBoost = 0.8f;

    var root = new BooleanQuery();

    // Helper to add a boosted query as SHOULD
    void AddShould(Query q, float boost)
    {
        q.Boost = boost;
        root.Add(q, Occur.SHOULD);
    }

    // === Ordered analyzed tokens (til shingles) ===
    var orderedTokens = AnalyzeToTerms(FieldContent, normalized);
    var orderedNoEmpty = orderedTokens
        .Where(t => !string.IsNullOrWhiteSpace(t))
        .ToList();

    // Distinct tokens (til term queries mm.)
    var tokens = orderedNoEmpty
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (tokens.Count == 0)
        return new BooleanQuery();

    // === A2) Sub-phrase shingles (bigrams/trigrams) ===
    void AddShinglePhrases(string field, float fieldBoost)
    {
        // Trigrams
        for (int i = 0; i <= orderedNoEmpty.Count - 3; i++)
        {
            var pq = new PhraseQuery { Slop = 0 };
            pq.Add(new Term(field, orderedNoEmpty[i]));
            pq.Add(new Term(field, orderedNoEmpty[i + 1]));
            pq.Add(new Term(field, orderedNoEmpty[i + 2]));

            float baseBoost = field == FieldTitle ? 20.0f : 10.0f;
            AddShould(pq, baseBoost * fieldBoost);
        }

        // Bigrams
        for (int i = 0; i <= orderedNoEmpty.Count - 2; i++)
        {
            var pq = new PhraseQuery { Slop = 0 };
            pq.Add(new Term(field, orderedNoEmpty[i]));
            pq.Add(new Term(field, orderedNoEmpty[i + 1]));

            float baseBoost = field == FieldTitle ? 12.0f : 6.0f;
            AddShould(pq, baseBoost * fieldBoost);
        }
    }

    AddShinglePhrases(FieldTitle, BoostTitle);
    AddShinglePhrases(FieldContent, BoostContent);

    // === A) Phrase queries (exact + loose) across fields ===
    if (tokens.Count >= 2)
    {
        PhraseQuery BuildPhrase(string field, int slop)
        {
            var pq = new PhraseQuery { Slop = slop };
            foreach (var t in tokens)
                pq.Add(new Term(field, t));
            return pq;
        }

        AddShould(BuildPhrase(FieldTitle, ExactPhraseSlop), ExactPhraseBoost * BoostTitle);
        AddShould(BuildPhrase(FieldFilename, ExactPhraseSlop), ExactPhraseBoost * BoostFilename);
        AddShould(BuildPhrase(FieldContent, ExactPhraseSlop), ExactPhraseBoost * BoostContent);

        AddShould(BuildPhrase(FieldTitle, LoosePhraseSlop), LoosePhraseBoost * BoostTitle);
        AddShould(BuildPhrase(FieldFilename, LoosePhraseSlop), LoosePhraseBoost * BoostFilename);
        AddShould(BuildPhrase(FieldContent, LoosePhraseSlop), LoosePhraseBoost * BoostContent);
    }

    // ... resten af din metode (AllTerms, TokenShoulds, Infix, MinimumNumberShouldMatch) uændret ...
    root.MinimumNumberShouldMatch = 1;
    return root;
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
        s = System.Text.RegularExpressions.Regex.Replace(s, @"[^\p{L}\p{Nd}]+", " ");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();
        return s;
    }
}
