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
    private readonly IndexedSearchService _searchService = new IndexedSearchService();

    [ObservableProperty] private string? selectedFolder;
    [ObservableProperty] private string status = "Vælge en mappe og søg.";
    
    public ObservableCollection<SearchResult> Results { get; } = new();

    [ObservableProperty]
    private SearchResult? selectedResult;

    [ObservableProperty]
    private string? query;
    
    partial void OnSelectedFolderChanged(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Directory.Exists(value))
        {
            Status = "Vælg en gyldig mappe.";
            return;
        }
        _ = IndexFolderAsync(value);
    }

    private async Task IndexFolderAsync(string folder)
    {
        try
        {
            Status = "Indekserer...";

            await _searchService.BuildIndexAsync(folder);

            Status = "Klar til søgning.";
        }
        catch
        {
            Status = "Kunne ikke indeksere mappen (nogle filer kunne ikke læses).";
        }
    }


    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedFolder))
        {
            Status = "Vælg først en mappe.";
            return;
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            Status = "Skriv en søgning.";
            return;
        }
        
        try
        {
            status = "Søger...";
            
            var found = await _searchService.SearchAsync(selectedFolder, query);
            Results.Clear();
            foreach (var r in found)
                Results.Add(r);

            SelectedResult = Results.FirstOrDefault();
            Status = $"Fandt {Results.Count} resultater.";
        }
        catch (Exception e)
        {
            Status = "Fejl under søgning.";
        }
    }
}