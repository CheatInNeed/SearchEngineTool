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
    private readonly NaiveSearchService _searchService = new();

    [ObservableProperty]
    private string? selectedFolder;

    [ObservableProperty]
    private string? query;

    [ObservableProperty]
    private string status = "Vælg en mappe og søg.";

    public ObservableCollection<SearchResult> Results { get; } = new();

    // Denne command kaldes fra UI når du trykker "Search"
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

        Status = $"Fandt {Results.Count} resultater.";
    }
    
    public Task BuildIndexForSelectedFolderAsync()
    {
        // Midlertidigt: NaiveSearchService bygger ikke index.
        // Vi tester bare PDF-extraction via SearchAsync.
        Status = "Klar til søgning (ingen indeks bruges i test-mode).";
        return Task.CompletedTask;
    }
}

// TODO 1: Når SelectedFolder sættes, disable "Search" indtil indexing er færdig (valgfrit)
// TODO 2: Vis "Indexing progress" (antal filer processeret / total) (valgfrit men fedt)
// TODO 3: Håndter hvis SearchAsync kaldes før index er bygget (returner tomt + status)