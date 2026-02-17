using Avalonia.Controls;
using Avalonia.Platform.Storage;
using FileSearchTool.ViewModels;
using System.Linq;
using System.Threading.Tasks;
using FileSearchTool.Services;
using Avalonia.Input;
using System;
using System.IO;
using Avalonia.Interactivity;
using FileSearchTool.ViewModels;

namespace FileSearchTool;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }

    private async void PickFolder_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var storage = StorageProvider;
        if (storage is null) return;

        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Vælg en mappe"
        });

        var folder = folders.FirstOrDefault();
        if (folder is null) return;

        if (DataContext is MainWindowViewModel vm)
        {
            vm.SelectedFolder = folder.Path.LocalPath;

            vm.Status = "Mappe valgt. Klar til søgning.";
        }
    }
    
    private void QueryBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainWindowViewModel vm)
        {
            if (vm.SearchCommand.CanExecute(null))
                vm.SearchCommand.Execute(null);

            e.Handled = true;
        }
    }
    
    private void Results_DoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (vm.SelectedResult == null) return;

        var path = vm.SelectedResult.FilePath;
        if (!string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
            return;

        var query = vm.Query ?? "";
        try
        {
            var previewVm = new PdfPreviewViewModel(path, query, startAtHit: vm.SelectedResult.HitNumber - 1);
            var win = new PdfPreviewWindow { DataContext = previewVm };
            win.Show();
        }
        catch (Exception ex)
        {
            // Minimal: log til console
            Console.WriteLine(ex);

            // Hvis du har en status-tekst i UI, kan du også vise den der.
        }
    }
}