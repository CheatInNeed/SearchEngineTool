using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileSearchTool.Models;
using FileSearchTool.Services;

namespace FileSearchTool.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IndexedSearchService _searchService = new IndexedSearchService();
    [ObservableProperty] private string? selectedFolder;
    [ObservableProperty] private string status = "Vælg en mappe og søg.";
    [ObservableProperty] private string? query;
    [ObservableProperty] private bool isResultsPaneOpen = true;
    [ObservableProperty] private bool isRecentFoldersOpen = false;
    [ObservableProperty] private PdfPreviewViewModel? currentPreview;
    public bool HasPreview => CurrentPreview != null;
    public bool HasNoPreview => CurrentPreview == null;
    
    [RelayCommand]
    private void ToggleResultsPane() => IsResultsPaneOpen = !IsResultsPaneOpen;

    [RelayCommand]
    private void ToggleRecentFolders() => IsRecentFoldersOpen = !IsRecentFoldersOpen;
    public ObservableCollection<SearchResultSummary> Results { get; } = new();
    
    [RelayCommand]
    private void OpenSelectedPreview()
    {
        if (SelectedResult == null) return;
        OpenPreviewForResult(SelectedResult);
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
            Status = "Klar til søgning.";
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

            Results.Clear();
            foreach (var r in found)
                Results.Add(r);

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
    
    private void OpenPreviewForResult(SearchResultSummary r)
    {
        if (string.IsNullOrWhiteSpace(Query)) return;

        var path = r.FilePath;
        if (!string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
            return;

        // start ALTID på bedste hit (0) – dit hit-list bliver sorteret i PdfPreviewViewModel
        CurrentPreview = new PdfPreviewViewModel(path, Query!, startAtHit: 0);
    }
    
    partial void OnCurrentPreviewChanged(PdfPreviewViewModel? value)
    {
        OnPropertyChanged(nameof(HasPreview));
        OnPropertyChanged(nameof(HasNoPreview));
    }
}
