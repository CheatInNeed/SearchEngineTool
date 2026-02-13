using System.IO;

namespace FileSearchTool.Models;

public record FolderItem(string FullPath)
{
    public string Name
    {
        get
        {
            var trimmed = FullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var name = Path.GetFileName(trimmed);
            return string.IsNullOrWhiteSpace(name) ? trimmed : name;
        }
    }
}