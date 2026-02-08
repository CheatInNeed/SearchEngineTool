using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FileSearchTool.Services;

public class QueryParser
{
    public QueryNode? Parse(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;

        // Materialisér tokens så vi kan indexe
        var tokens = TokenizeQuery(query).ToList();
        if (tokens.Count == 0) return null;
        
        for (int j = 0; j < tokens.Count; j++)
        {
            tokens[j] = tokens[j].Trim();
            if (tokens[j].Length == 0) return null; // ekstra safety

            if (isAnd(tokens[j]) || isOr(tokens[j]))
                tokens[j] = tokens[j].ToUpperInvariant();
            else
                tokens[j] = tokens[j].ToLowerInvariant();
        }

        int i = 0;

        bool IsOp(string t) => isAnd(t) || isOr(t);

        QueryNode? ParseAndGroup()
        {
            // term (AND term)*
            if (i >= tokens.Count) return null;
            if (IsOp(tokens[i])) return null; // må ikke starte på AND/OR

            QueryNode node = new TermNode(tokens[i]);
            i++;

            while (i < tokens.Count && isAnd(tokens[i]))
            {
                i++; // skip AND

                // skal komme et term efter AND
                if (i >= tokens.Count) return null;
                if (IsOp(tokens[i])) return null;

                QueryNode right = new TermNode(tokens[i]);
                i++;

                node = new AndNode(node, right);
            }

            return node;
        }

        // Start: første AND-gruppe
        QueryNode? root = ParseAndGroup();
        if (root is null) return null;

        // OR binder grupper sammen: AndGroup (OR AndGroup)*
        while (i < tokens.Count)
        {
            // forvent OR her (ellers invalid query som "cat dog" eller "cat AND OR dog")
            if (!isOr(tokens[i])) return null;
            i++; // skip OR

            QueryNode? right = ParseAndGroup();
            if (right is null) return null;

            root = new OrNode(root, right);
        }

        return root;
    }

    private static bool isAnd(string t)
    {
        return t.Equals("AND", StringComparison.OrdinalIgnoreCase);
    }

    private static bool isOr(string t)
    {
        return t.Equals("OR", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> TokenizeQuery(string query)
    {
        var currentQuery = new StringBuilder();

        foreach (char ch in query)
        {
            if (char.IsLetterOrDigit(ch))
            {
                currentQuery.Append(char.ToLowerInvariant(ch));
            }
            else
            {
                if (currentQuery.Length > 0)
                {
                    var tok = currentQuery.ToString();
                    if (isAnd(tok) || isOr(tok))
                        yield return tok.ToUpperInvariant();
                    else
                        yield return tok;

                    currentQuery.Clear();
                }
            }
        }

        if (currentQuery.Length > 0)
        {
            var tok = currentQuery.ToString();
            if (isAnd(tok) || isOr(tok))
                yield return tok.ToUpperInvariant();
            else
                yield return tok;
        }
    }
}
