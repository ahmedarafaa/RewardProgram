using Microsoft.AspNetCore.Hosting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RewardProgram.Application.Interfaces;
using ZXing.OneD;

namespace RewardProgram.Infrastructure.Services;

public class BarcodePdfGenerator : IBarcodePdfGenerator
{
    // Zebra label: 50mm x 25mm — one label per page
    private const float LabelWidthMm = 50f;
    private const float LabelHeightMm = 25f;
    private const float PaddingMm = 1f;

    // Logo sits top-left with the product name beside it; the barcode spans the full
    // label width underneath, centred. Giving the barcode the whole width is what
    // makes the bars printable — beside a 16mm logo they were 1.18 printer dots wide,
    // narrower than the head can resolve, so they printed as broken grey texture.
    private const float TopStripHeightMm = 11f;

    // The badge is taller than it is wide (aspect ~0.79), so it is sized by width and
    // sets its own height inside the strip.
    private const float LogoWidthMm = 8.5f;
    private const float LogoGapMm = 1.5f;

    // The barcode is drawn as vector rectangles, not a bitmap. A bitmap has to be
    // resampled to the printer's dot grid, which left ~35% of a scanline through the
    // bars as mid-grey — neither black nor white. A thermal head cannot print grey, so
    // it either drops or dithers those pixels, which is what "faded" looks like on
    // paper. Vector edges are resolved by the printer's own RIP instead.
    private const float BarcodeHeightMm = 8f;

    // Code 128 requires a clear margin of at least 10 modules each side. The old bitmap
    // only got one by accident, from how ZXing centred the symbol in the image.
    private const int QuietZoneModules = 10;

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
                        .Column(label =>
                        {
                            // Top strip: logo at the left, product name centred beside it.
                            label.Item()
                                .Height(TopStripHeightMm, Unit.Millimetre)
                                .Row(strip =>
                                {
                                    if (logo is not null)
                                    {
                                        strip.ConstantItem(LogoWidthMm, Unit.Millimetre)
                                            .AlignMiddle()
                                            .Image(logo).FitWidth();

                                        strip.ConstantItem(LogoGapMm, Unit.Millimetre);
                                    }

                                    strip.RelativeItem()
                                        .AlignMiddle()
                                        .AlignCenter()
                                        .Text(productName)
                                        .FontSize(5)
                                        .FontColor(Colors.Black);
                                });

                            // Barcode across the full label width, then the readable code.
                            label.Item()
                                .PaddingTop(0.4f, Unit.Millimetre)
                                .Height(BarcodeHeightMm, Unit.Millimetre)
                                .Element(barcode => DrawBarcode(barcode, code));

                            label.Item()
                                .PaddingTop(0.2f, Unit.Millimetre)
                                .AlignCenter()
                                .Text(code)
                                .FontSize(5)
                                .FontColor(Colors.Black);
                        });
                });
            }
        });

        return document.GeneratePdf();
    }

    // Lays the symbol out as one relative-width cell per run of equal modules, so the
    // barcode still fills the column exactly as FitWidth used to — but every bar edge
    // is a vector boundary the printer resolves itself, rather than a smear of grey
    // pixels baked in by resampling a bitmap.
    private static void DrawBarcode(IContainer container, string code)
    {
        var modules = new Code128Writer().encode(code);

        container.Row(row =>
        {
            row.RelativeItem(QuietZoneModules);

            var i = 0;
            while (i < modules.Length)
            {
                var j = i;
                while (j < modules.Length && modules[j] == modules[i])
                    j++;

                var run = row.RelativeItem(j - i);
                if (modules[i])
                    run.Background(Colors.Black);

                i = j;
            }

            row.RelativeItem(QuietZoneModules);
        });
    }
}
