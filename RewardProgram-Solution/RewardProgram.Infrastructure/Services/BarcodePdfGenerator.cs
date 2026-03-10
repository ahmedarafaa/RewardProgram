using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RewardProgram.Application.Interfaces;
using ZXing;
using ZXing.Common;

namespace RewardProgram.Infrastructure.Services;

public class BarcodePdfGenerator : IBarcodePdfGenerator
{
    private const int ColumnsPerRow = 3;
    private const float LabelWidthMm = 64f;
    private const float LabelHeightMm = 30f;
    private const float PageMarginMm = 8f;
    private const float LabelPaddingMm = 2f;
    private const int BarcodeImageWidth = 300;
    private const int BarcodeImageHeight = 80;

    private static readonly BarcodeWriterPixelData BarcodeWriter = new()
    {
        Format = BarcodeFormat.CODE_128,
        Options = new EncodingOptions
        {
            Width = BarcodeImageWidth,
            Height = BarcodeImageHeight,
            Margin = 2,
            PureBarcode = true
        }
    };

    public byte[] GeneratePdf(string productName, string productCode, List<string> barcodeCodes)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(PageMarginMm, Unit.Millimetre);

                page.Content().Column(column =>
                {
                    var chunks = barcodeCodes.Chunk(ColumnsPerRow);

                    foreach (var row in chunks)
                    {
                        column.Item().Row(r =>
                        {
                            foreach (var code in row)
                            {
                                r.ConstantItem(LabelWidthMm, Unit.Millimetre)
                                    .Height(LabelHeightMm, Unit.Millimetre)
                                    .Border(0.3f)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .Padding(LabelPaddingMm, Unit.Millimetre)
                                    .Column(label =>
                                    {
                                        label.Item()
                                            .AlignCenter()
                                            .Text(productName)
                                            .FontSize(7)
                                            .FontColor(Colors.Black);

                                        label.Item()
                                            .AlignCenter()
                                            .PaddingVertical(1, Unit.Millimetre)
                                            .Image(GenerateBarcodeImage(code));

                                        label.Item()
                                            .AlignCenter()
                                            .Text(code)
                                            .FontSize(7)
                                            .FontColor(Colors.Black);
                                    });
                            }

                            // Fill remaining cells in incomplete rows
                            var remaining = ColumnsPerRow - row.Count();
                            for (var i = 0; i < remaining; i++)
                            {
                                r.ConstantItem(LabelWidthMm, Unit.Millimetre)
                                    .Height(LabelHeightMm, Unit.Millimetre);
                            }
                        });
                    }
                });
            });
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
