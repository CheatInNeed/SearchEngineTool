using Avalonia.Controls;
using Avalonia.Platform.Storage;
using FileSearchTool.ViewModels;
using System.Linq;
using Avalonia.Input;
using System;
using Avalonia.Interactivity;

namespace FileSearchTool;

public partial class MainWindow : Window
{
    private Canvas? _mainOverlay;
    private PdfPreviewViewModel? _hookedPreview;
    private bool _isDraggingSidebar;
    private double _sidebarDragStartX;
    private double _sidebarDragStartWidth;

    public MainWindow()
    {
        InitializeComponent();

        DataContext = new MainWindowViewModel();

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
        _mainOverlay = this.FindControl<Canvas>("MainOverlayCanvas");
        
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

        foreach (var (r, isActive) in _hookedPreview.CurrentPageHitRectsPixelsWithActive)
        {
            var rect = new Avalonia.Controls.Shapes.Rectangle
            {
                Width = r.Width,
                Height = r.Height,
                Fill = new Avalonia.Media.SolidColorBrush(
                    isActive
                        ? Avalonia.Media.Color.FromArgb(165, 90, 200, 255)
                        : Avalonia.Media.Color.FromArgb(75, 90, 200, 255)
                ),
                Stroke = new Avalonia.Media.SolidColorBrush(
                    isActive
                        ? Avalonia.Media.Color.FromArgb(140, 160, 235, 255)
                        : Avalonia.Media.Color.FromArgb(60, 160, 235, 255)
                ),
                StrokeThickness = isActive ? 1.0 : 0.0
            };

            Canvas.SetLeft(rect, r.X);
            Canvas.SetTop(rect, r.Y);
            _mainOverlay.Children.Add(rect);
        }
    }

    private void SidebarSplitter_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        _isDraggingSidebar = true;
        _sidebarDragStartX = e.GetPosition(MainLayoutGrid).X;
        _sidebarDragStartWidth = vm.SidebarWidth;
        e.Pointer.Capture(SidebarSplitter);
        e.Handled = true;
    }

    private void SidebarSplitter_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDraggingSidebar || DataContext is not MainWindowViewModel vm) return;

        var currentX = e.GetPosition(MainLayoutGrid).X;
        vm.SetSidebarWidthFromDrag(_sidebarDragStartWidth + currentX - _sidebarDragStartX);
        e.Handled = true;
    }

    private void SidebarSplitter_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDraggingSidebar) return;

        _isDraggingSidebar = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }
}
