using Microsoft.AspNetCore.Hosting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RewardProgram.Application.Interfaces;
using ZXing;
using ZXing.Common;

namespace RewardProgram.Infrastructure.Services;

public class BarcodePdfGenerator : IBarcodePdfGenerator
{
    // Zebra label: 50mm x 25mm — one label per page
    private const float LabelWidthMm = 50f;
    private const float LabelHeightMm = 25f;
    private const float PaddingMm = 1.5f;

    // Logo fills the left column (vertically centered); the product name, barcode,
    // and human-readable code stack in the right column beside it.
    private const float LogoColumnWidthMm = 16f;
    private const float ColumnGapMm = 1.5f;

    // The barcode lives in the narrower right column. Generated at enough
    // resolution (~3px/module) so ZXing renders crisp, well-separated bars;
    // FitWidth then scales it to the column.
    private const int BarcodeImageWidth = 600;
    private const int BarcodeImageHeight = 150;

    // Drop-in brand logo at wwwroot/images/barcode-logo.{png,jpg,jpeg}. Swappable
    // without a rebuild; if absent, labels simply render without a logo.
    private static readonly string[] LogoFileNames =
        ["barcode-logo.png", "barcode-logo.jpg", "barcode-logo.jpeg"];

    // Loaded once (singleton service) — null when no logo file is present.
    private readonly Lazy<byte[]?> _logo;

    public BarcodePdfGenerator(IWebHostEnvironment environment)
    {
        _logo = new Lazy<byte[]?>(
            () => LoadLogo(environment),
            System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private static byte[]? LoadLogo(IWebHostEnvironment environment)
    {
        var webRoot = environment.WebRootPath
            ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        var imagesDir = Path.Combine(webRoot, "images");

        foreach (var name in LogoFileNames)
        {
            var path = Path.Combine(imagesDir, name);
            if (File.Exists(path))
                return File.ReadAllBytes(path);
        }

        return null;
    }

    [ThreadStatic]
    private static BarcodeWriterPixelData? _barcodeWriter;

    private static BarcodeWriterPixelData BarcodeWriter => _barcodeWriter ??= new()
    {
        Format = BarcodeFormat.CODE_128,
        Options = new EncodingOptions
        {
            Width = BarcodeImageWidth,
            Height = BarcodeImageHeight,
            Margin = 0,
            PureBarcode = true
        }
    };

    public byte[] GeneratePdf(string productName, string productCode, List<string> barcodeCodes)
    {
        var logo = _logo.Value;

        var document = Document.Create(container =>
        {
            foreach (var code in barcodeCodes)
            {
                container.Page(page =>
                {
                    page.Size(LabelWidthMm, LabelHeightMm, Unit.Millimetre);
                    page.Margin(PaddingMm, Unit.Millimetre);

                    page.Content()
                        .AlignMiddle()
                        .Row(row =>
                        {
                            // Logo fills the left column, vertically centered.
                            if (logo is not null)
                            {
                                row.ConstantItem(LogoColumnWidthMm, Unit.Millimetre)
                                    .Height(LabelHeightMm - 2 * PaddingMm, Unit.Millimetre)
                                    .AlignMiddle()
                                    .AlignCenter()
                                    .Image(logo).FitArea();
                                row.ConstantItem(ColumnGapMm, Unit.Millimetre);
                            }

                            // Right column: product name, barcode, then the human-readable code.
                            row.RelativeItem()
                                .AlignMiddle()
                                .Column(data =>
                                {
                                    data.Item()
                                        .AlignCenter()
                                        .Text(productName)
                                        .FontSize(5)
                                        .FontColor(Colors.Black);

                                    data.Item()
                                        .PaddingVertical(0.4f, Unit.Millimetre)
                                        .AlignCenter()
                                        .Image(GenerateBarcodeImage(code)).FitWidth();

                                    data.Item()
                                        .AlignCenter()
                                        .Text(code)
                                        .FontSize(5)
                                        .FontColor(Colors.Black);
                                });
                        });
                });
            }
        });

        return document.GeneratePdf();
    }

    private static byte[] GenerateBarcodeImage(string code)
    {
        var pixelData = BarcodeWriter.Write(code);
        return ConvertToBmp(pixelData.Pixels, pixelData.Width, pixelData.Height);
    }

    private static byte[] ConvertToBmp(byte[] rgbaPixels, int width, int height)
    {
        // BMP with 24-bit color (no alpha) — simplest format QuestPDF can read
        var rowSize = (width * 3 + 3) & ~3; // rows padded to 4-byte boundary
        var imageSize = rowSize * height;
        var fileSize = 54 + imageSize;

        using var ms = new MemoryStream(fileSize);
        using var bw = new BinaryWriter(ms);

        // BMP header
        bw.Write((byte)'B');
        bw.Write((byte)'M');
        bw.Write(fileSize);
        bw.Write(0); // reserved
        bw.Write(54); // pixel data offset

        // DIB header (BITMAPINFOHEADER)
        bw.Write(40); // header size
        bw.Write(width);
        bw.Write(height);
        bw.Write((short)1); // planes
        bw.Write((short)24); // bits per pixel
        bw.Write(0); // no compression
        bw.Write(imageSize);
        bw.Write(0); // horizontal resolution
        bw.Write(0); // vertical resolution
        bw.Write(0); // colors in palette
        bw.Write(0); // important colors

        // Pixel data (BMP is bottom-to-top)
        var padding = new byte[rowSize - width * 3];
        for (var y = height - 1; y >= 0; y--)
        {
            for (var x = 0; x < width; x++)
            {
                var i = (y * width + x) * 4; // RGBA
                bw.Write(rgbaPixels[i + 2]); // B
                bw.Write(rgbaPixels[i + 1]); // G
                bw.Write(rgbaPixels[i + 0]); // R
            }
            if (padding.Length > 0)
                bw.Write(padding);
        }

        return ms.ToArray();
    }
}
