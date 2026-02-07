using System.Collections.Generic;
using System.Text;

namespace FileSearchTool.Services;
public static class Tokenizer {
    public static IEnumerable<string> Tokenize(string text) {
        if (string.IsNullOrEmpty(text))
            yield break;

        var current = new StringBuilder();

        foreach (char ch in text) {
            if (char.IsLetter(ch)) {
                current.Append(char.ToLower(ch));
            } else {
                if (current.Length > 0) {
                    yield return current.ToString();
                    current.Clear();
                }
            }
        }

        if (current.Length > 0) {
            yield return current.ToString();
        }
    }
}