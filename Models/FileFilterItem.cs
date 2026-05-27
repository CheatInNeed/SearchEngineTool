using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FileSearchTool.Models;

public partial class FileFilterItem : ObservableObject
{
    public FileFilterItem(string filePath, int resultCount)
    {
        FilePath = filePath;
        ResultCount = resultCount;
    }

    public string FilePath { get; }
    public string FileName => Path.GetFileName(FilePath);
    public string FolderName => Path.GetFileName(Path.GetDirectoryName(FilePath) ?? "");
    public string DisplayName => $"{FileName} ({ResultCount})";

    [ObservableProperty] private bool isIncluded = true;
    [ObservableProperty] private int resultCount;

    partial void OnResultCountChanged(int value)
    {
        OnPropertyChanged(nameof(DisplayName));
    }
}
