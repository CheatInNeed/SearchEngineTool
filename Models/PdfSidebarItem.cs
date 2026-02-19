namespace FileSearchTool.Models;

public record PdfSidebarItem(
    string Title,
    string Context,
    int HitListIndex,
    int PageNumber,
    float Score)
{
    public string ScoreText => $"{Score:0.0}x";
}