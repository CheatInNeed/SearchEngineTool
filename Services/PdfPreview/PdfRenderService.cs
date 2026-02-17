using System;
using System.IO;
using Avalonia.Media.Imaging;
using PDFtoImage;
using SkiaSharp;

namespace FileSearchTool.Services.PdfPreview;

public static class PdfRenderService
{
    // Returnerer: (Avalonia bitmap, pixelW, pixelH)
    public static (Bitmap bmp, int pixelW, int pixelH) RenderPage(string pdfPath, int pageNumber)
    {
        var pageIndex = pageNumber - 1;

        if (!(OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() || OperatingSystem.IsLinux()))
            throw new PlatformNotSupportedException("PDF preview understøttes ikke på denne platform.");

        try
        {
#pragma warning disable CA1416
            var pdfBytes = File.ReadAllBytes(pdfPath);
            var pdfBase64 = Convert.ToBase64String(pdfBytes);

            using SKBitmap skBmp = Conversion.ToImage(pdfBase64, pageIndex);
#pragma warning restore CA1416

            using var image = SKImage.FromBitmap(skBmp);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var ms = new MemoryStream(data.ToArray());

            var avaloniaBmp = new Bitmap(ms);
            return (avaloniaBmp, avaloniaBmp.PixelSize.Width, avaloniaBmp.PixelSize.Height);
        }
        catch (Exception ex)
        {
            // så du kan se fejlen i console/log
            throw new Exception($"PDF render fejlede for '{pdfPath}' side {pageNumber}.", ex);
        }
    }
}