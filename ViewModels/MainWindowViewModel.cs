using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileSearchTool.Models;
using FileSearchTool.Services;

namespace FileSearchTool.ViewModels;

// ObservableObject giver dig INotifyPropertyChanged automatisk

/*
 * Når folder vælges, byg index direkte og sæt status “Indexing…”.
 * (Hint: kald BuildIndexAsync i PickFolder_Click eller i VM når SelectedFolder ændres.)
 */
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IndexedSearchService _searchService = new();

    [ObservableProperty]
    private string? selectedFolder;

    [ObservableProperty]
    private string? query;

    [ObservableProperty]
    private string status = "Vælg en mappe og søg.";

    public ObservableCollection<SearchResult> Results { get; } = new();

    // Denne command kaldes fra UI når du trykker "Search"
    [RelayCommand]
    private async System.Threading.Tasks.Task SearchAsync()
    {
        Results.Clear();

        if (string.IsNullOrWhiteSpace(SelectedFolder))
        {
            Status = "Ingen mappe valgt.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Query))
        {
            Status = "Skriv en query.";
            return;
        }

        Status = "Søger (indexed)...";
        var found = await _searchService.SearchAsync(Query);

        foreach (var r in found)
            Results.Add(r);

        Status = $"Fandt {Results.Count} resultater.";
    }
    
    public async Task BuildIndexForSelectedFolderAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedFolder))
            return;

        await _searchService.BuildIndexAsync(SelectedFolder);
    }
}