using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileSearchTool.Models;

namespace FileSearchTool.Services;

public class IndexedSearchService
{
    private readonly FileCrawler _crawler = new();
    private readonly InvertedIndex _index = new();
    private readonly QueryParser _parser = new();

    private readonly List<string> _docPaths = new();
    
    public Task BuildIndexAsync(string rootFolder)
    {
        return Task.Run(() =>
        {
            _index.Clear();
            _docPaths.Clear();

            // 2) Find filer
            var files = _crawler.GetFiles(rootFolder);

            try
            {
                foreach (var path in files)
                {
                    int docId = _docPaths.Count;
                    _docPaths.Add(path);
                    string text = File.ReadAllText(path);
                    var tokens = Tokenizer.Tokenize(text);
                    _index.Add(docId, tokens);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        });
    }
    
    public Task<List<SearchResult>> SearchAsync(string query)
    {
        return Task.Run(() =>
        {
            var results = new List<SearchResult>();

            if (string.IsNullOrEmpty(query))
            {
                return results;
            }

            var node = _parser.Parse(query);
            if (node==null)
            {
                return results;
            }

            var evaluator = new QueryEvaluator(_index);
            var docIds = evaluator.Evaluate(node);
            
            foreach (var docId in docIds)
            {
                var path = _docPaths[docId];
                var lines = GetLinesFromFile(path);

                var terms = Tokenizer.Tokenize(query);
                var snippet = SnippetMaker.MakeLineSnippet(lines, terms);

                results.Add(new SearchResult(path, snippet));
            }

            return results;
        });
    }

    private string[] GetLinesFromFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();

        switch (ext)
        {
            case ".txt":
            case ".md":
            case ".cs":
                return File.ReadAllLines(path);

            case ".pdf":
            // TODO senere:
            // var text = ExtractTextFromPdf(path);
            // return text.Split('\n');

            default:
                return Array.Empty<string>();
        }
    }
}
