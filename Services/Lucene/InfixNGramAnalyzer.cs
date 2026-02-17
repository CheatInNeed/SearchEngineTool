using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Analysis.NGram;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Util;

namespace FileSearchTool.Services.Lucene;

/// <summary>
/// Index-time analyzer that generates ngrams (infix search).
/// Query-time: we DO NOT analyze into ngrams; we query this field with TermQuery on the raw token.
/// </summary>
public sealed class InfixNGramAnalyzer : Analyzer
{
    private readonly LuceneVersion _version;
    private readonly int _minGram;
    private readonly int _maxGram;

    public InfixNGramAnalyzer(LuceneVersion version, int minGram = 3, int maxGram = 15)
    {
        _version = version;
        _minGram = minGram;
        _maxGram = maxGram;
    }

    protected override TokenStreamComponents CreateComponents(string fieldName, System.IO.TextReader reader)
    {
        // StandardTokenizer splits words nicely; then we lowercase; then we create infix ngrams.
        var tokenizer = new StandardTokenizer(_version, reader);
        TokenStream stream = new LowerCaseFilter(_version, tokenizer);

        // NGramTokenFilter creates all substrings of each token.
        stream = new NGramTokenFilter(_version, stream, _minGram, _maxGram);

        return new TokenStreamComponents(tokenizer, stream);
    }
}