using System;
using System.IO;
using Avalonia.Media.Imaging;
using PDFtoImage;
using SkiaSharp;

namespace FileSearchTool.Services.PdfPreview;

public static class PdfRenderService
{
    // Returnerer: (Avalonia bitmap, pixelW, pixelH)
    public static (Bitmap bmp, int pixelW, int pixelH) RenderPage(
        string pdfPath,
        int pageNumber,
        float scale = 1.0f)
    {
        var pageIndex = pageNumber - 1;

        if (!(OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() || OperatingSystem.IsLinux()))
            throw new PlatformNotSupportedException("PDF preview understøttes ikke på denne platform.");

        try
        {
#pragma warning disable CA1416
            var pdfBytes = File.ReadAllBytes(pdfPath);
// Brug byte[] overload i stedet for base64
            var options = new RenderOptions
            {
                // 72 DPI er standard PDF
                Dpi = (int)(72 * scale)   // scale = 1.0f, 0.8f, 1.5f osv.
            };

            using SKBitmap skBmp = Conversion.ToImage(pdfBytes, pageIndex, null, options);
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