using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileSearchTool.Models;
using FileSearchTool.Services;

namespace FileSearchTool.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IndexedSearchService _searchService = new IndexedSearchService();
    private readonly FileCrawler _fileCrawler = new FileCrawler();
    [ObservableProperty] private string? selectedFolder;
    [ObservableProperty] private string status = "Vælg en mappe og søg.";
    [ObservableProperty] private string? query;
    [ObservableProperty] private bool isResultsPaneOpen = true;
    [ObservableProperty] private bool isRecentFoldersOpen = false;
    [ObservableProperty] private bool isFileFilterOpen = false;
    [ObservableProperty] private double sidebarWidth = 430;
    [ObservableProperty] private bool isHitsPaneCollapsed = false;
    [ObservableProperty] private double hitsPaneHeight = 240;
    [ObservableProperty] private PdfPreviewViewModel? currentPreview;
    private readonly List<SearchResultSummary> _allResults = new();
    private bool _isUpdatingFileFilters;
    public bool HasPreview => CurrentPreview != null;
    public bool HasNoPreview => CurrentPreview == null;
    public bool HasResults => Results.Count > 0;
    public GridLength SidebarGridLength => IsResultsPaneOpen ? new GridLength(SidebarWidth) : new GridLength(0);
    public GridLength SplitterGridLength => IsResultsPaneOpen ? new GridLength(10) : new GridLength(0);
    public GridLength HitsPaneGridLength => IsHitsPaneCollapsed ? new GridLength(42) : new GridLength(HitsPaneHeight);
    public GridLength HitsSplitterGridLength => IsHitsPaneCollapsed ? new GridLength(0) : new GridLength(10);
    public string HitsToggleText => IsHitsPaneCollapsed ? "▴" : "▾";
    public bool IsHitsListVisible => !IsHitsPaneCollapsed;
    public string ResultCountText => Results.Count == 0 ? "No results yet" : $"{Results.Count} results";
    public string FileFilterButtonText => FileFilters.Count == 0
        ? "Files"
        : $"{FileFilters.Count(x => x.IsIncluded)}/{FileFilters.Count} files";
    public string SupportedFileTypesText => "PDF, TXT, Markdown, DOCX";
    
    [RelayCommand]
    private void ToggleResultsPane() => IsResultsPaneOpen = !IsResultsPaneOpen;

    public void SetSidebarWidthFromDrag(double width)
    {
        SidebarWidth = Math.Clamp(width, 320, 760);
    }

    public void SetHitsPaneHeightFromDrag(double height)
    {
        HitsPaneHeight = Math.Clamp(height, 100, 600);
    }

    [RelayCommand]
    private void ToggleHitsPane()
    {
        IsHitsPaneCollapsed = !IsHitsPaneCollapsed;
    }

    partial void OnSidebarWidthChanged(double value)
    {
        OnPropertyChanged(nameof(SidebarGridLength));
    }

    partial void OnIsResultsPaneOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(SidebarGridLength));
        OnPropertyChanged(nameof(SplitterGridLength));
    }

    partial void OnHitsPaneHeightChanged(double value)
    {
        OnPropertyChanged(nameof(HitsPaneGridLength));
    }

    partial void OnIsHitsPaneCollapsedChanged(bool value)
    {
        OnPropertyChanged(nameof(HitsPaneGridLength));
        OnPropertyChanged(nameof(HitsSplitterGridLength));
        OnPropertyChanged(nameof(HitsToggleText));
        OnPropertyChanged(nameof(IsHitsListVisible));
    }

    [RelayCommand]
    private void ToggleRecentFolders() => IsRecentFoldersOpen = !IsRecentFoldersOpen;

    [RelayCommand]
    private void ToggleFileFilter() => IsFileFilterOpen = !IsFileFilterOpen;

    public ObservableCollection<SearchResultSummary> Results { get; } = new();
    public ObservableCollection<FileFilterItem> FileFilters { get; } = new();
    
    [RelayCommand]
    private void OpenSelectedPreview()
    {
        if (SelectedResult == null) return;
        OpenResult(SelectedResult);
    }

    [ObservableProperty] private SearchResultSummary? selectedResult;

    // XAML bruger disse:
    public ObservableCollection<FolderItem> RecentFolders { get; } = new();

    [ObservableProperty] private FolderItem? selectedRecentFolder;

    partial void OnSelectedFolderChanged(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Directory.Exists(value))
        {
            Status = "Vælg en gyldig mappe.";
            return;
        }

        AddRecentFolder(value);
        RebuildFileFiltersFromFolder(value);

        _ = IndexFolderAsync(value);
    }

    partial void OnSelectedRecentFolderChanged(FolderItem? value)
    {
        if (value == null) return;

        if (!string.Equals(SelectedFolder, value.FullPath, StringComparison.OrdinalIgnoreCase))
            SelectedFolder = value.FullPath;
    }

    private void AddRecentFolder(string folder)
    {
        // undgå duplicates
        var existing = RecentFolders.FirstOrDefault(f =>
            string.Equals(f.FullPath, folder, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            // flyt til toppen
            RecentFolders.Remove(existing);
            RecentFolders.Insert(0, existing);
            SelectedRecentFolder = existing;
            return;
        }

        var item = new FolderItem(folder);
        RecentFolders.Insert(0, item);
        SelectedRecentFolder = item;
    }

    private async Task IndexFolderAsync(string folder)
    {
        try
        {
            Status = "Tjekker index...";
            await _searchService.BuildIndexAsync(folder);
            Status = "Klar til søgning. (Hvis analyzer ændres: slet index folder og re-index.)";
        }
        catch
        {
            Status = "Kunne ikke indeksere mappen (nogle filer kunne ikke læses).";
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedFolder))
        {
            Status = "Vælg først en mappe.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Query))
        {
            Status = "Skriv en søgning.";
            return;
        }

        try
        {
            Status = "Søger...";

            var found = await _searchService.SearchSummaryAsync(SelectedFolder, Query);

            _allResults.Clear();
            _allResults.AddRange(found);
            UpdateFileFilterResultCounts(_allResults);
            ApplyResultFilter();

            // Vælg bedste dokument (allerøverst)
            SelectedResult = Results.FirstOrDefault();
            Status = $"Fandt {Results.Count} dokumenter.";

            // 7) pdfpreview skal altid vise bedste hit totalt set
            var best = SelectedResult;
            if (best != null && string.Equals(Path.GetExtension(best.FilePath), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                OpenPreviewForResult(best);
            }
            else
            {
                // hvis top ikke er pdf: find bedste pdf i listen
                var bestPdf = Results.FirstOrDefault(x =>
                    string.Equals(Path.GetExtension(x.FilePath), ".pdf", StringComparison.OrdinalIgnoreCase));

                if (bestPdf != null)
                    OpenPreviewForResult(bestPdf);
                else
                    CurrentPreview = null;
            }
        }
        catch
        {
            Status = "Fejl under søgning.";
        }
    }

    [RelayCommand]
    private void SelectAllFiles()
    {
        SetAllFileFilters(true);
    }

    [RelayCommand]
    private void ClearFileFilters()
    {
        SetAllFileFilters(false);
    }

    private void SetAllFileFilters(bool value)
    {
        _isUpdatingFileFilters = true;
        try
        {
            foreach (var item in FileFilters)
                item.IsIncluded = value;
        }
        finally
        {
            _isUpdatingFileFilters = false;
        }

        ApplyResultFilter();
    }

    private void RebuildFileFiltersFromFolder(string folder)
    {
        foreach (var item in FileFilters)
            item.PropertyChanged -= FileFilterOnPropertyChanged;

        FileFilters.Clear();

        foreach (var item in _fileCrawler.GetFiles(folder)
                     .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                     .Select(path => new FileFilterItem(path, 0)))
        {
            item.PropertyChanged += FileFilterOnPropertyChanged;
            FileFilters.Add(item);
        }

        OnPropertyChanged(nameof(FileFilterButtonText));
    }

    private void UpdateFileFilterResultCounts(IEnumerable<SearchResultSummary> results)
    {
        var counts = results
            .GroupBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        foreach (var item in FileFilters)
            item.ResultCount = counts.TryGetValue(item.FilePath, out var count) ? count : 0;
    }

    private void FileFilterOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isUpdatingFileFilters || e.PropertyName != nameof(FileFilterItem.IsIncluded))
            return;

        ApplyResultFilter();
    }

    private void ApplyResultFilter()
    {
        var included = FileFilters
            .Where(x => x.IsIncluded)
            .Select(x => x.FilePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Results.Clear();
        foreach (var result in _allResults.Where(x => included.Contains(x.FilePath)))
            Results.Add(result);

        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ResultCountText));
        OnPropertyChanged(nameof(FileFilterButtonText));

        SelectedResult = Results.FirstOrDefault();
    }
    
    private void OpenPreviewForResult(SearchResultSummary r)
    {
        if (string.IsNullOrWhiteSpace(Query)) return;

        var path = r.FilePath;
        if (!string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
            return;

        // start ALTID på bedste hit (0) – dit hit-list bliver sorteret i PdfPreviewViewModel
        CurrentPreview = new PdfPreviewViewModel(path, Query!, startAtHit: 0, preferredPage: r.PageNumber);
    }

    private void OpenResult(SearchResultSummary r)
    {
        var path = r.FilePath;
        if (string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            OpenPreviewForResult(r);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            Status = $"Åbnede {Path.GetFileName(path)}.";
        }
        catch
        {
            Status = "Kunne ikke åbne filen.";
        }
    }
    
    partial void OnCurrentPreviewChanged(PdfPreviewViewModel? value)
    {
        OnPropertyChanged(nameof(HasPreview));
        OnPropertyChanged(nameof(HasNoPreview));
    }
}
