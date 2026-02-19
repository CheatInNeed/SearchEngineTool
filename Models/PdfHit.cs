using Avalonia;

namespace FileSearchTool.Models;

public record PdfHit(
    int HitIndex,
    int PageNumber,
    Rect[] Boxes,
    float Score,
    string MatchedToken,
    string Context);