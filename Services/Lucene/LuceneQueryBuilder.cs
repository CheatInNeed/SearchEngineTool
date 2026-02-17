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
    private const string FieldContent = "content";

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
        var tokens = AnalyzeToTerms(FieldContent, normalized);

        if (tokens.Count == 0)
            return new BooleanQuery();

        var root = new BooleanQuery();

        // A) Phrase query (boost high) if query has 2+ tokens
        if (tokens.Count >= 2)
        {
            var phrase = new PhraseQuery();

            // Keep phrase fairly strict (slop = 0)
            // Later we can allow small slop like 1-2.
            phrase.Slop = 0;

            foreach (var t in tokens)
                phrase.Add(new Term(FieldContent, t));

            phrase.Boost = 12.0f;
            root.Add(phrase, Occur.SHOULD);
        }

        // B) Token queries (boost lower)
        var tokenBool = new BooleanQuery();

        foreach (var t in tokens)
        {
            var termQuery = new TermQuery(new Term(FieldContent, t));
            termQuery.Boost = 1.0f;
            tokenBool.Add(termQuery, Occur.SHOULD);
        }

        // MinimumShouldMatch: if many tokens, require some to match
        // This prevents "one random word" matching from dominating.
        if (tokens.Count >= 4)
        {
            // Example: 4-5 tokens => require 2; 6+ tokens => require 3
            tokenBool.MinimumNumberShouldMatch = tokens.Count >= 6 ? 3 : 2;
        }
        
        // C) AND-query: kræv at ALLE tokens findes (men stadig som SHOULD i root)
        var mustAll = new BooleanQuery();
        foreach (var t in tokens.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            mustAll.Add(new TermQuery(new Term(FieldContent, t)), Occur.MUST);
        }
        mustAll.Boost = 6.0f;
        root.Add(mustAll, Occur.SHOULD);

        root.Add(tokenBool, Occur.SHOULD);

        // If everything is SHOULD, Lucene will allow documents that match none.
        // So require at least one SHOULD clause to match by setting MinimumShouldMatch on root.
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
