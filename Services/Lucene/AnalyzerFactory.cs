using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Analysis.Miscellaneous;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Analysis.Util;
using Lucene.Net.Util;
using Lucene.Net.Analysis.Synonym;

namespace FileSearchTool.Services.Lucene;

public static class AnalyzerFactory
{
    public static Analyzer Create(LuceneVersion version)
    {
        var resourcesDir = Path.Combine(AppContext.BaseDirectory, "Resources");

        var stopEn = LoadStopwords(Path.Combine(resourcesDir, "stopwords_en.txt"), ignoreIfMissing: true);
        var stopDa = LoadStopwords(Path.Combine(resourcesDir, "stopwords_da.txt"), ignoreIfMissing: true);

        // Ét synonymmap (bruges både index + query, fordi I bruger samme analyzer begge steder)
        var synonymsPath = Path.Combine(resourcesDir, "synonyms.txt");
        //var synonymMap = SynonymLoader.TryLoad(version, synonymsPath);
        SynonymMap? synonymMap = null; // synonyms slået fra

        // Standard analyzer til title/filename/content
        var standard = new ClassicTextAnalyzer(version, stopEn, stopDa, synonymMap);

        // Infix analyzer kun til contentInfix (som nu)
        var perField = new Dictionary<string, Analyzer>
        {
            { "contentInfix", new InfixNGramAnalyzer(version, 3, 15) }
        };

        return new PerFieldAnalyzerWrapper(standard, perField);
    }

    private static CharArraySet LoadStopwords(string path, bool ignoreIfMissing)
    {
        try
        {
            if (!File.Exists(path))
            {
                if (ignoreIfMissing) return CharArraySet.EMPTY_SET;
                throw new FileNotFoundException($"Stopwords file not found: {path}");
            }

            var lines = File.ReadAllLines(path, Encoding.UTF8)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0 && !x.StartsWith("#"))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            return new CharArraySet(LuceneVersion.LUCENE_48, lines, ignoreCase: true);
        }
        catch
        {
            return CharArraySet.EMPTY_SET;
        }
    }

    /// <summary>
    /// "Classic production-ish" analyzer:
    /// StandardTokenizer -> LowerCase -> StopFilter(EN+DA) -> Synonyms(optional) -> Snowball stemming (EN + DA if available)
    /// </summary>
    private sealed class ClassicTextAnalyzer : Analyzer
    {
        private readonly LuceneVersion _version;
        private readonly CharArraySet _stop;
        private readonly SynonymMap? _synonyms;

        public ClassicTextAnalyzer(LuceneVersion version, CharArraySet stopEn, CharArraySet stopDa, SynonymMap? synonyms)
        {
            _version = version;
            _stop = CharArraySet.UnmodifiableSet(CharArraySet.Copy(version, stopEn.Concat(stopDa)));
            _synonyms = synonyms;
        }

        protected override TokenStreamComponents CreateComponents(string fieldName, TextReader reader)
        {
            var tokenizer = new StandardTokenizer(_version, reader);
            TokenStream stream = new LowerCaseFilter(_version, tokenizer);

            if (_stop != null && _stop.Count > 0)
                stream = new StopFilter(_version, stream, _stop);

            if (_synonyms != null)
                stream = SynonymLoader.ApplySynonymsOrFallback(stream, _synonyms);

            // Snowball stemming: English + (try) Danish
            stream = SnowballStemTry(stream, "English");
            stream = SnowballStemTry(stream, "Danish");

            return new TokenStreamComponents(tokenizer, stream);
        }

        private TokenStream SnowballStemTry(TokenStream input, string language)
        {
            try
            {
                // Undgå compile-fejl hvis Ext-stemmers ikke findes: reflection på stemmer-klassen
                var stemmerType = Type.GetType($"Lucene.Net.Analysis.Snowball.Ext.{language}Stemmer, Lucene.Net.Analysis.Common");
                if (stemmerType == null)
                    stemmerType = Type.GetType($"Lucene.Net.Analysis.Snowball.Ext.{language}Stemmer");

                // Nogle builds bruger {language}Stemmer, andre {language}Stemmer (navne varierer),
                // vi prøver også {language}Stemmer / {language}Stemmer
                if (stemmerType == null)
                    stemmerType = Type.GetType($"Lucene.Net.Analysis.Snowball.Ext.{language}Stemmer");

                if (stemmerType == null)
                {
                    // Alternativ: {language}Stemmer hedder ofte {language}Stemmer? (forskelle i packages)
                    stemmerType = AppDomain.CurrentDomain.GetAssemblies()
                        .Select(a => a.GetType($"Lucene.Net.Analysis.Snowball.Ext.{language}Stemmer"))
                        .FirstOrDefault(t => t != null);
                }

                // Hvis vi ikke finder en stemmer, gør vi ingenting.
                if (stemmerType == null)
                    return input;

                // SnowballFilter findes i Lucene.Net.Analysis.Snowball
                var snowballFilterType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("Lucene.Net.Analysis.Snowball.SnowballFilter"))
                    .FirstOrDefault(t => t != null);

                if (snowballFilterType == null)
                    return input;

                var stemmer = Activator.CreateInstance(stemmerType);
                var filter = (TokenStream)Activator.CreateInstance(snowballFilterType, _version, input, stemmer)!;
                return filter;
            }
            catch
            {
                return input;
            }
        }
    }
}
