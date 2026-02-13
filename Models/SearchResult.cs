using System.IO;
namespace FileSearchTool.Models;

public record SearchResult(string FilePath, string Snippet, int HitNumber)
{
    public string FileName => Path.GetFileName(FilePath);
}