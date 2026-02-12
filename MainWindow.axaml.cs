using Avalonia.Controls;
using Avalonia.Platform.Storage;
using FileSearchTool.ViewModels;
using System.Linq;
using System.Threading.Tasks;
using FileSearchTool.Services;

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
}