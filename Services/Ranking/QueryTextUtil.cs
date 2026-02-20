using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FileSearchTool.Services.Ranking;

public static class QueryTextUtil
{
    private static readonly Regex NonWord = new(@"[^\p{L}\p{Nd}]+", RegexOptions.Compiled);
    private static readonly Regex MultiSpace = new(@"\s+", RegexOptions.Compiled);

    public static string Normalize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.ToLowerInvariant();
        s = NonWord.Replace(s, " ");
        s = MultiSpace.Replace(s, " ").Trim();
        return s;
    }

    public static List<string> Tokenize(string s)
    {
        s = Normalize(s);
        return s.Length == 0
            ? new List<string>()
            : s.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
    }
}