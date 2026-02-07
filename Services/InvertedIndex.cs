using System.Collections.Generic;
using System.Linq;

namespace FileSearchTool.Services;

public class InvertedIndex
{
    // term -> docIds
    private readonly Dictionary<string, HashSet<int>> _postings = new();
    public void Add(int docId, IEnumerable<string> tokens)
    {
        foreach (var t in tokens)
        {
            if (!_postings.TryGetValue(t.ToLowerInvariant(), out var set))
            {
                set = new HashSet<int>();
                _postings[t.ToLowerInvariant()] = set;
            }

            // HashSet sørger selv for at docId kun findes én gang
            set.Add(docId);
        }
    }
    
    public bool ContainsTerm(string term)
    {
        return _postings.ContainsKey(term.ToLowerInvariant());
    }

    public IReadOnlyCollection<int> GetDocs(string term)
    {
        term = term.ToLowerInvariant();

        return _postings.TryGetValue(term, out var set)
            ? set
            : (IReadOnlyCollection<int>)System.Array.Empty<int>();
    }
    
    public void Clear()
    {
        _postings.Clear();
    }
    
    public int TermCount
    {
        get
        {
            return _postings.Keys.Count;
        }
    }
}