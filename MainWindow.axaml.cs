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
    
    private Canvas? _mainOverlay;
    private PdfPreviewViewModel? _hookedPreview;
    
    public MainWindow()
    {
        InitializeComponent();

        DataContext = new MainWindowViewModel();

        _mainOverlay = this.FindControl<Canvas>("MainOverlayCanvas");

        if (DataContext is MainWindowViewModel vm)
        {
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainWindowViewModel.CurrentPreview))
                    HookPreview(vm.CurrentPreview);
            };

            HookPreview(vm.CurrentPreview);
        }
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

        if (vm.OpenSelectedPreviewCommand.CanExecute(null))
            vm.OpenSelectedPreviewCommand.Execute(null);
    }
    
    private void HookPreview(PdfPreviewViewModel? preview)
    {
        if (_hookedPreview != null)
            _hookedPreview.OverlayChanged -= PreviewOnOverlayChanged;

        _hookedPreview = preview;

        if (_hookedPreview != null)
            _hookedPreview.OverlayChanged += PreviewOnOverlayChanged;

        RedrawMainOverlay();
    }

    private void PreviewOnOverlayChanged(object? sender, EventArgs e) => RedrawMainOverlay();

    private void RedrawMainOverlay()
    {
        if (_mainOverlay == null) return;
        _mainOverlay.Children.Clear();

        if (_hookedPreview == null) return;

        foreach (var r in _hookedPreview.CurrentPageHitRectsPixels)
        {
            var rect = new Avalonia.Controls.Shapes.Rectangle
            {
                Width = r.Width,
                Height = r.Height,
                Fill = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(120, 255, 255, 0)),
                StrokeThickness = 0
            };

            Canvas.SetLeft(rect, r.X);
            Canvas.SetTop(rect, r.Y);
            _mainOverlay.Children.Add(rect);
        }
    }
}