namespace FileSearchTool.Models;

public record SnippetPart(string Text, bool IsHighlight)
{
    public string FontWeight => IsHighlight ? "Bold" : "Normal";
    public string Background => IsHighlight ? "#55FFD966" : "#00000000";
}
