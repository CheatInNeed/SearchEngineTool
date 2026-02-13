using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileSearchTool.Models;
using FileSearchTool.Services;

namespace FileSearchTool.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly NaiveSearchService _searchService = new();

    private const int MaxRecentFolders = 10;
    
    private bool _syncingSelection;

    public ObservableCollection<FolderItem> RecentFolders { get; } = new();
    [ObservableProperty]
    private string? selectedFolder;

    partial void OnSelectedFolderChanged(string? value)
    {
        if (_syncingSelection) return;
        if (string.IsNullOrWhiteSpace(value)) return;

        AddRecentFolder(value);

        try
        {
            _syncingSelection = true;

            // sync list selection til folder
            SelectedRecentFolder = RecentFolders.FirstOrDefault(f =>
                string.Equals(f.FullPath, value, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _syncingSelection = false;
        }

        _ = SaveRecentFoldersAsync();
    }

    [ObservableProperty]
    private string? query;

    [ObservableProperty]
    private string status = "Vælg en mappe og søg.";

    public ObservableCollection<SearchResult> Results { get; } = new();

    [ObservableProperty]
    private SearchResult? selectedResult;

    public MainWindowViewModel()
    {
        _ = LoadRecentFoldersAsync();
    }

    private static string GetAppDataDir()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(baseDir, "FileSearchTool");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string RecentFoldersPath => Path.Combine(GetAppDataDir(), "recent-folders.json");

    private void AddRecentFolder(string folder)
    {
        var existing = RecentFolders.FirstOrDefault(f =>
            string.Equals(f.FullPath, folder, StringComparison.OrdinalIgnoreCase));
        if (existing != null) RecentFolders.Remove(existing);

        RecentFolders.Insert(0, new FolderItem(folder));

        while (RecentFolders.Count > MaxRecentFolders)
            RecentFolders.RemoveAt(RecentFolders.Count - 1);
    }

    private async Task LoadRecentFoldersAsync()
    {
        try
        {
            if (!File.Exists(RecentFoldersPath)) return;

            var json = await File.ReadAllTextAsync(RecentFoldersPath);
            var items = JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();

            RecentFolders.Clear();
            foreach (var f in items.Where(Directory.Exists))
                RecentFolders.Add(new FolderItem(f));

            // auto-vælg seneste folder (valgfrit)
            if (SelectedFolder == null && RecentFolders.Count > 0)
                SelectedFolder = RecentFolders[0].FullPath;
        }
        catch
        {
            // swallow: UX feature må ikke crashe app
        }
    }

    private async Task SaveRecentFoldersAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(
                RecentFolders.Select(f => f.FullPath).ToArray(),
                new JsonSerializerOptions { WriteIndented = true }
            );
            await File.WriteAllTextAsync(RecentFoldersPath, json);
        }
        catch
        {
        }
    }

    [RelayCommand]
    private void UseRecentFolder(string folder)
    {
        if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            SelectedFolder = folder;
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedFolder))
        {
            Status = "Vælg først en mappe.";
            return;
        }

        Status = "Søger...";
        var found = await _searchService.SearchAsync(SelectedFolder, Query);

        Results.Clear();
        foreach (var r in found)
            Results.Add(r);

        SelectedResult = Results.FirstOrDefault();
        Status = $"Fandt {Results.Count} resultater.";
    }
    
    [ObservableProperty]
    private FolderItem? selectedRecentFolder;

    partial void OnSelectedRecentFolderChanged(FolderItem? value)
    {
        if (_syncingSelection) return;
        if (value == null) return;

        if (!Directory.Exists(value.FullPath))
            return;

        try
        {
            _syncingSelection = true;

            // sync folder til list selection
            SelectedFolder = value.FullPath;
        }
        finally
        {
            _syncingSelection = false;
        }
    }
}
