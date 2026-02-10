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

    // Normalisér tokens (men lad phrases "..." være urørte her)
    for (int j = 0; j < tokens.Count; j++)
    {
        tokens[j] = tokens[j].Trim();
        if (tokens[j].Length == 0) return null;

        // Phrase tokens beholdes (MakeNodeFromToken normaliserer indholdet)
        if (tokens[j].Length >= 2 && tokens[j].StartsWith("\"") && tokens[j].EndsWith("\""))
            continue;

        if (isAnd(tokens[j]) || isOr(tokens[j]))
            tokens[j] = tokens[j].ToUpperInvariant();
        else
            tokens[j] = tokens[j].ToLowerInvariant();
    }

    int i = 0;

    bool IsOp(string t) => isAnd(t) || isOr(t);

    QueryNode MakeNodeFromToken(string t)
    {
        if (t.Length >= 2 && t.StartsWith("\"") && t.EndsWith("\""))
        {
            var phrase = t.Substring(1, t.Length - 2).Trim().ToLowerInvariant();
            return new PhraseNode(phrase);
        }

        return new TermNode(t);
    }

    QueryNode? ParseAndGroup()
    {
        // atom (AND atom)*
        if (i >= tokens.Count) return null;
        if (IsOp(tokens[i])) return null; // må ikke starte på AND/OR

        QueryNode node = MakeNodeFromToken(tokens[i]);
        i++;

        while (i < tokens.Count && isAnd(tokens[i]))
        {
            i++; // skip AND

            if (i >= tokens.Count) return null;
            if (IsOp(tokens[i])) return null;

            QueryNode right = MakeNodeFromToken(tokens[i]);
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
        var tokens = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        void FlushToken()
        {
            if (sb.Length == 0) return;

            var tok = sb.ToString().Trim();
            sb.Clear();

            if (tok.Length == 0) return;

            if (inQuotes)
            {
                // Marker phrase ved at beholde "" rundt om
                tokens.Add($"\"{tok}\"");
                return;
            }

            if (isAnd(tok) || isOr(tok))
                tokens.Add(tok.ToUpperInvariant());
            else
                tokens.Add(tok.ToLowerInvariant());
        }

        for (int i = 0; i < query.Length; i++)
        {
            char ch = query[i];

            if (ch == '"')
            {
                // skift quote-tilstand, og flush det der er samlet op indtil nu
                FlushToken();
                inQuotes = !inQuotes;
                continue;
            }

            if (inQuotes)
            {
                // inde i quotes: behold alt (inkl mellemrum)
                sb.Append(ch);
            }
            else
            {
                // uden for quotes: kun bogstaver/tal bygger token, alt andet splitter
                if (char.IsLetterOrDigit(ch))
                    sb.Append(ch);
                else
                    FlushToken();
            }
        }

        // flush sidste token
        FlushToken();

        return tokens;
    }
}
