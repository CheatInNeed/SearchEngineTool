using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileSearchTool.Models;
using FileSearchTool.Services;

namespace FileSearchTool.ViewModels;

// ObservableObject giver dig INotifyPropertyChanged automatisk
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

        Status = "Søger...";
        var found = await _searchService.SearchAsync(SelectedFolder, Query);

        foreach (var r in found)
            Results.Add(r);

        Status = $"Fandt {Results.Count} resultater.";
    }
}