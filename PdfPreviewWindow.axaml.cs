using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using FileSearchTool.ViewModels;
using System;

namespace FileSearchTool;

public partial class PdfPreviewWindow : Window
{
    private Canvas? _overlay;
    private PdfPreviewViewModel? _hookedVm;

    public PdfPreviewWindow()
    {
        InitializeComponent();
        _overlay = this.FindControl<Canvas>("OverlayCanvas");

        DataContextChanged += (_, __) =>
        {
            // unhook gammel VM
            if (_hookedVm != null)
                _hookedVm.OverlayChanged -= VmOnOverlayChanged;

            _hookedVm = DataContext as PdfPreviewViewModel;

            // hook ny VM
            if (_hookedVm != null)
                _hookedVm.OverlayChanged += VmOnOverlayChanged;

            RedrawOverlay();
        };

        // hvis DataContext allerede er sat før vinduet vises
        _hookedVm = DataContext as PdfPreviewViewModel;
        if (_hookedVm != null)
            _hookedVm.OverlayChanged += VmOnOverlayChanged;

        RedrawOverlay();
    }

    private void VmOnOverlayChanged(object? sender, EventArgs e) => RedrawOverlay();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void RedrawOverlay()
    {
        if (_overlay == null) return;
        _overlay.Children.Clear();

        if (DataContext is not PdfPreviewViewModel vm) return;

        foreach (var r in vm.CurrentPageHitRectsPixels)
        {
            var rect = new Rectangle
            {
                Width = r.Width,
                Height = r.Height,
                Fill = new SolidColorBrush(Color.FromArgb(120, 255, 255, 0)),
                StrokeThickness = 0
            };

            Canvas.SetLeft(rect, r.X);
            Canvas.SetTop(rect, r.Y);
            _overlay.Children.Add(rect);
        }
    }

    protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (DataContext is not PdfPreviewViewModel vm) return;

        if (e.Key == Avalonia.Input.Key.Left)
        {
            vm.PrevHitCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Avalonia.Input.Key.Right)
        {
            vm.NextHitCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Avalonia.Input.Key.PageDown || e.Key == Avalonia.Input.Key.Down)
        {
            vm.NextPageCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Avalonia.Input.Key.PageUp || e.Key == Avalonia.Input.Key.Up)
        {
            vm.PrevPageCommand.Execute(null);
            e.Handled = true;
        }
        
        var mods = e.KeyModifiers;

        if (mods.HasFlag(Avalonia.Input.KeyModifiers.Meta)) // Meta = Cmd på mac
        {
            if (e.Key == Avalonia.Input.Key.OemPlus || e.Key == Avalonia.Input.Key.Add)
            {
                vm.ZoomInCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (e.Key == Avalonia.Input.Key.OemMinus || e.Key == Avalonia.Input.Key.Subtract)
            {
                vm.ZoomOutCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (e.Key == Avalonia.Input.Key.D0 || e.Key == Avalonia.Input.Key.NumPad0)
            {
                vm.ZoomResetCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }
    }
}
