using RewardProgram.Application.Interfaces;
using ZXing;
using ZXing.Common;

namespace RewardProgram.Infrastructure.Services;

public class QrCodeGenerator : IQrCodeGenerator
{
    [ThreadStatic]
    private static BarcodeWriterPixelData? _writer;

    public byte[] Generate(string content, int width = 300, int height = 300)
    {
        _writer ??= new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new EncodingOptions
            {
                Width = width,
                Height = height,
                Margin = 1
            }
        };

        var pixelData = _writer.Write(content);
        return ConvertToPng(pixelData.Pixels, pixelData.Width, pixelData.Height);
    }

    private static byte[] ConvertToPng(byte[] rgbaPixels, int width, int height)
    {
        // BMP with 24-bit color — same pattern as BarcodePdfGenerator
        var rowSize = (width * 3 + 3) & ~3;
        var imageSize = rowSize * height;
        var fileSize = 54 + imageSize;

        using var ms = new MemoryStream(fileSize);
        using var bw = new BinaryWriter(ms);

        // BMP header
        bw.Write((byte)'B');
        bw.Write((byte)'M');
        bw.Write(fileSize);
        bw.Write(0);
        bw.Write(54);

        // DIB header
        bw.Write(40);
        bw.Write(width);
        bw.Write(height);
        bw.Write((short)1);
        bw.Write((short)24);
        bw.Write(0);
        bw.Write(imageSize);
        bw.Write(0);
        bw.Write(0);
        bw.Write(0);
        bw.Write(0);

        // Pixel data (bottom-to-top)
        var padding = new byte[rowSize - width * 3];
        for (var y = height - 1; y >= 0; y--)
        {
            for (var x = 0; x < width; x++)
            {
                var i = (y * width + x) * 4;
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
