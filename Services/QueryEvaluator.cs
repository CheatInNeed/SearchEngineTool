using System;
using System.Collections.Generic;
using System.Linq;

namespace FileSearchTool.Services;

public class QueryEvaluator
{
    private readonly InvertedIndex _index;

    public QueryEvaluator(InvertedIndex index)
    {
        _index = index;
    }

    public IReadOnlyCollection<int> Evaluate(QueryNode node)
    {
        if (node == null)
            return new HashSet<int>();

        switch (node)
        {
            case TermNode termNode:
                return new HashSet<int>(_index.GetDocs(termNode.Term));

            case AndNode andNode:
            {
                var left = new HashSet<int>(Evaluate(andNode.Left));
                var right = new HashSet<int>(Evaluate(andNode.Right));
                left.IntersectWith(right);
                return left;
            }
            case OrNode orNode:
            {
                var left = new HashSet<int>(Evaluate(orNode.Left));
                var right = new HashSet<int>(Evaluate(orNode.Right));
                left.UnionWith(right);
                return left;
            }

            default:
                throw new ArgumentException("Ukendt QueryNode-type");
        }
    }


    // TODO (valgfrit):
    // - Hvis du senere vil score/rangere, kan Evaluate returnere Dictionary<int, score>
}