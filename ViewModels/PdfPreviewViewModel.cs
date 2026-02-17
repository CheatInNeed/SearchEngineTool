using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileSearchTool.Models;
using FileSearchTool.Services.PdfPreview;
using UglyToad.PdfPig;

namespace FileSearchTool.ViewModels;

public partial class PdfPreviewViewModel : ObservableObject
{
    public event EventHandler? OverlayChanged;

    private readonly string _pdfPath;
    private readonly List<PdfHit> _hits;

    [ObservableProperty] private Bitmap? pageBitmap;
    [ObservableProperty] private int currentPage = 1;
    
    [ObservableProperty] private float zoom = 0.8f;

    // Pixel-størrelse (til overlay canvas)
    [ObservableProperty] private double pagePixelWidth;
    [ObservableProperty] private double pagePixelHeight;

    // PDF “points” størrelse (til konvertering)
    private double _pagePdfWidth;
    private double _pagePdfHeight;

    [ObservableProperty] private int currentHitIndex = 0;

    [RelayCommand]
    private void ZoomIn()
    {
        Zoom = MathF.Min(3.0f, Zoom * 1.15f);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        Zoom = MathF.Max(0.3f, Zoom / 1.15f);
    }

    [RelayCommand]
    private void ZoomReset()
    {
        Zoom = 0.8f;
    }
    public string FileName => Path.GetFileName(_pdfPath);

    public string HitStatus =>
        _hits.Count == 0 ? "0 hits" : $"{CurrentHitIndex + 1}/{_hits.Count} (Side {CurrentPage})";

    // Pixel rects for current page
    public IReadOnlyList<Rect> CurrentPageHitRectsPixels { get; private set; } = Array.Empty<Rect>();

    public PdfPreviewViewModel(string pdfPath, string rawQuery, int? startAtHit = null)
    {
        _pdfPath = pdfPath;

        _hits = PdfHitFinder.FindAllHits(pdfPath, rawQuery);

        if (_hits.Count == 0)
        {
            CurrentPage = 1;
            Render();
            return;
        }

        CurrentHitIndex = Math.Clamp(startAtHit ?? 0, 0, _hits.Count - 1);
        CurrentPage = _hits[CurrentHitIndex].PageNumber;
        Render();
        UpdateOverlay();
    }

    [RelayCommand]
    private void NextHit()
    {
        if (_hits.Count == 0) return;
        CurrentHitIndex = (CurrentHitIndex + 1) % _hits.Count;
        JumpToHit();
    }

    [RelayCommand]
    private void PrevHit()
    {
        if (_hits.Count == 0) return;
        CurrentHitIndex = (CurrentHitIndex - 1 + _hits.Count) % _hits.Count;
        JumpToHit();
    }

    private void JumpToHit()
    {
        var hit = _hits[CurrentHitIndex];
        if (CurrentPage != hit.PageNumber)
        {
            CurrentPage = hit.PageNumber;
            Render();
        }
        UpdateOverlay();
        OnPropertyChanged(nameof(HitStatus));
    }

    private void Render()
    {
        var (bmp, pxW, pxH) = PdfRenderService.RenderPage(_pdfPath, CurrentPage, Zoom);

        PageBitmap = bmp;
        PagePixelWidth = pxW;
        PagePixelHeight = pxH;

        using var doc = PdfDocument.Open(_pdfPath);
        var page = doc.GetPage(CurrentPage);
        _pagePdfWidth = page.Width;
        _pagePdfHeight = page.Height;

        UpdateOverlay();

        OnPropertyChanged(nameof(HitStatus));
    }

    private void UpdateOverlay()
    {
        // Alle hits på current page
        var pageHits = _hits.Where(h => h.PageNumber == CurrentPage).ToList();

        // konverter PDF coords -> pixels
        // PDF coords: origin bottom-left
        // Pixel coords: origin top-left
        var sx = PagePixelWidth / _pagePdfWidth;
        var sy = PagePixelHeight / _pagePdfHeight;

        var rects = new List<Rect>();

        foreach (var h in pageHits)
        {
            foreach (var b in h.Boxes)
            {
                var x = b.X * sx;
                var yFromBottom = b.Y * sy;

                var w = b.Width * sx;
                var hpx = b.Height * sy;

                // flip Y:
                var y = PagePixelHeight - (yFromBottom + hpx);

                rects.Add(new Rect(x, y, w, hpx));
            }
        }

        CurrentPageHitRectsPixels = rects;
        OverlayChanged?.Invoke(this, EventArgs.Empty);
    }
    
    partial void OnZoomChanged(float value)
    {
        // clamp så man ikke kan zoome til 0 eller alt for meget
        if (value < 0.3f) Zoom = 0.3f;
        if (value > 3.0f) Zoom = 3.0f;

        Render();
    }
}
