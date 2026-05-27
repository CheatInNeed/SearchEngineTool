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
using System.Collections.ObjectModel;
using FileSearchTool.Services.Lucene;
using Lucene.Net.Util;

namespace FileSearchTool.ViewModels;

public partial class PdfPreviewViewModel : ObservableObject
{
    public event EventHandler? OverlayChanged;
    public ObservableCollection<PdfSidebarItem> SidebarItems { get; } = new();
    
    public IReadOnlyList<(Rect rect, bool isActive)> CurrentPageHitRectsPixelsWithActive { get; private set; }
        = Array.Empty<(Rect rect, bool isActive)>();

    private readonly string _pdfPath;
    private readonly List<PdfHit> _hits;
    private readonly Dictionary<int, List<int>> _hitIndexesByPage = new();
    private readonly int _pageCount;

    [ObservableProperty] private Bitmap? pageBitmap;
    [ObservableProperty] private int currentPage = 1;
    
    [ObservableProperty] private PdfSidebarItem? selectedSidebarItem;
    
    [ObservableProperty] private float zoom = 1.0f;

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
        Zoom = 1.0f;
    }
    
    [RelayCommand]
    private void NextPage()
    {
        if (_pageCount <= 0) return;

        var newPage = Math.Min(_pageCount, CurrentPage + 1);
        if (newPage == CurrentPage) return;

        GoToPage(newPage, preferFirstHit: true);
    }

    [RelayCommand]
    private void PrevPage()
    {
        if (_pageCount <= 0) return;

        var newPage = Math.Max(1, CurrentPage - 1);
        if (newPage == CurrentPage) return;

        GoToPage(newPage, preferFirstHit: false);
    }

    public string PageStatus => $"{CurrentPage}/{_pageCount}";
    public string FileName => Path.GetFileName(_pdfPath);

    public string HitStatus =>
        _hits.Count == 0 ? $"0 hits (Side {CurrentPage})"
            : $"{CurrentHitIndex + 1}/{_hits.Count} (Side {_hits[CurrentHitIndex].PageNumber})";

    public PdfPreviewViewModel(string pdfPath, string rawQuery, int? startAtHit = null, int? preferredPage = null)
    {
        _pdfPath = pdfPath;

        var analyzedTokens = new LuceneQueryBuilder(AnalyzerFactory.Create(LuceneVersion.LUCENE_48))
            .AnalyzeQueryTerms(rawQuery);

        _hits = PdfHitFinder.FindAllHits(pdfPath, rawQuery, analyzedTokens);
        
        _hits.Sort((a, b) =>
        {
            int c = b.Score.CompareTo(a.Score);
            if (c != 0) return c;
            c = a.PageNumber.CompareTo(b.PageNumber);
            if (c != 0) return c;
            return a.HitIndex.CompareTo(b.HitIndex);
        });
        
        using (var doc = PdfDocument.Open(_pdfPath))
        {
            _pageCount = doc.NumberOfPages;
        }

// Map page -> liste af hitIndex i _hits (index i listen)
        for (int i = 0; i < _hits.Count; i++)
        {
            var p = _hits[i].PageNumber;
            if (!_hitIndexesByPage.TryGetValue(p, out var list))
            {
                list = new List<int>();
                _hitIndexesByPage[p] = list;
            }
            list.Add(i);
        }
        
        BuildSidebarItemsAllHits();

        if (_hits.Count == 0)
        {
            CurrentPage = preferredPage.HasValue
                ? Math.Clamp(preferredPage.Value, 1, Math.Max(1, _pageCount))
                : 1;
            Render();
            return;
        }

        var preferredHitIndex = preferredPage.HasValue
            ? _hits.FindIndex(h => h.PageNumber == preferredPage.Value)
            : -1;

        CurrentHitIndex = preferredHitIndex >= 0
            ? preferredHitIndex
            : Math.Clamp(startAtHit ?? 0, 0, _hits.Count - 1);
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

    public void JumpToHit()
    {
        if (_hits.Count == 0) return;

        var hit = _hits[CurrentHitIndex];

        if (CurrentPage != hit.PageNumber)
        {
            // hop side og render
            CurrentPage = hit.PageNumber;
            Render();
        }

        UpdateOverlay();
        OnPropertyChanged(nameof(HitStatus));
        OnPropertyChanged(nameof(PageStatus));
        
        SelectedSidebarItem = SidebarItems.FirstOrDefault(s => s.HitListIndex == CurrentHitIndex)
                              ?? SelectedSidebarItem;
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
        var pageHits = _hits.Where(h => h.PageNumber == CurrentPage).ToList();

        var sx = PagePixelWidth / _pagePdfWidth;
        var sy = PagePixelHeight / _pagePdfHeight;

        var active = (_hits.Count > 0 && CurrentHitIndex >= 0 && CurrentHitIndex < _hits.Count)
            ? _hits[CurrentHitIndex]
            : null;

        var rectsWithFlags = new List<(Rect rect, bool isActive)>();

        foreach (var h in pageHits)
        {
            foreach (var b in h.Boxes)
            {
                var x = b.X * sx;
                var yFromBottom = b.Y * sy;

                var w = b.Width * sx;
                var hpx = b.Height * sy;

                var y = PagePixelHeight - (yFromBottom + hpx);

                bool isActive = active != null
                                && active.PageNumber == CurrentPage
                                && h.HitIndex == active.HitIndex;

                rectsWithFlags.Add((new Rect(x, y, w, hpx), isActive));
            }
        }

        CurrentPageHitRectsPixelsWithActive = rectsWithFlags;
        OverlayChanged?.Invoke(this, EventArgs.Empty);
    }

    
    partial void OnZoomChanged(float value)
    {
        // clamp så man ikke kan zoome til 0 eller alt for meget
        if (value < 0.3f) Zoom = 0.3f;
        if (value > 3.0f) Zoom = 3.0f;

        Render();
    }
    
    private void GoToPage(int pageNumber, bool preferFirstHit)
    {
        CurrentPage = pageNumber;

        // Vælg et hit på den side, hvis der findes hits
        if (_hitIndexesByPage.TryGetValue(pageNumber, out var hitList) && hitList.Count > 0)
        {
            CurrentHitIndex = preferFirstHit ? hitList[0] : hitList[^1];
        }

        Render();

        OnPropertyChanged(nameof(PageStatus));
    }
    
    partial void OnSelectedSidebarItemChanged(PdfSidebarItem? value)
    {
        if (value == null) return;
        CurrentHitIndex = Math.Clamp(value.HitListIndex, 0, _hits.Count - 1);
        JumpToHit();
    }
    
    private void BuildSidebarItemsAllHits()
    {
        SidebarItems.Clear();
        if (_hits.Count == 0) return;

        var ordered = _hits
            .Select((h, idx) => (hit: h, idx))
            .OrderByDescending(x => x.hit.Score)      // boost først
            .ThenBy(x => x.hit.PageNumber)
            .ThenBy(x => x.idx)
            .ToList();

        foreach (var x in ordered)
        {
            var title = $"Side {x.hit.PageNumber}  ·  {x.hit.MatchedToken}  ·  {x.hit.Score:0.0}x";
            SidebarItems.Add(new PdfSidebarItem(title, x.hit.Context, x.idx, x.hit.PageNumber, x.hit.Score));
        }

        // sync selection
        SelectedSidebarItem = SidebarItems.FirstOrDefault(s => s.HitListIndex == CurrentHitIndex)
                              ?? SidebarItems.FirstOrDefault();
    }


}
