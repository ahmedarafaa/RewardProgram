using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Admin.ErpCustomers;
using RewardProgram.Application.Contracts.Admin.Imports;
using RewardProgram.Application.Contracts.Admin.Products;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Helpers;
using RewardProgram.Infrastructure.Services;

namespace RewardProgram.Infrastructure.Tests.Services;

/// <summary>
/// Each template and its parser are generated from and matched against the same
/// column map, and these tests are what hold that promise: a template downloaded,
/// filled in and uploaded unchanged must import with zero failures.
/// </summary>
public class ImportTemplateTests
{
    private readonly ProductImportReader _productReader = new();
    private readonly ErpCustomerImportReader _customerReader = new();

    // Builds a real template workbook through the real exporter. The localizer is
    // stubbed to echo keys — the data sheet's headers come from the column map, not
    // from resources, which is exactly the property under test.
    private static MemoryStream BuildTemplate(ImportTemplateDefinition definition)
    {
        var stream = new MemoryStream();
        new ExcelExporter()
            .WriteMultiSheetAsync(stream, b => ImportTemplateBuilder.Build(b, new EchoLocalizer(), definition))
            .GetAwaiter().GetResult();
        stream.Position = 0;
        return stream;
    }

    public static TheoryData<string> Templates => new("products", "erp-customers");

    private static ImportTemplateDefinition Definition(string key) =>
        key == "products" ? ImportTemplates.Products : ImportTemplates.ErpCustomers;

    [Theory]
    [MemberData(nameof(Templates))]
    public void Template_FirstSheet_ShouldHoldOnlyTheHeaderRow(string key)
    {
        var definition = Definition(key);
        using var template = BuildTemplate(definition);
        using var workbook = new XLWorkbook(template);

        var sheet = workbook.Worksheets.First();
        sheet.RowsUsed().Should().HaveCount(1);
        sheet.Row(1).CellsUsed().Select(c => c.GetString())
            .Should().Equal(definition.Columns.Select(c => c.CanonicalHeader));

        // The instructions live on a separate sheet, after the data sheet, so the
        // importer (which reads worksheet one) never sees them.
        workbook.Worksheets.Count.Should().Be(2);
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void EveryCanonicalHeader_ShouldBeAnAliasOfItsOwnColumn(string key)
    {
        // Guards the one way a template and its parser could still disagree: a
        // canonical header edited without adding it to that column's alias set.
        var columns = Definition(key).Columns;

        foreach (var column in columns)
        {
            var normalized = ImportColumns.NormalizeHeader(column.CanonicalHeader);
            ImportColumns.Match(columns, normalized)
                .Should().BeSameAs(column, "'{0}' must resolve back to its own column", column.CanonicalHeader);
        }
    }

    [Fact]
    public void ProductTemplate_ShouldParseWithNoDataRows()
    {
        using var template = BuildTemplate(ImportTemplates.Products);

        // A template uploaded untouched yields no products at all — no sample row
        // that would land in the catalogue as a phantom product.
        _productReader.Read(template, 100).Should().BeEmpty();
    }

    [Fact]
    public void CustomerTemplate_ShouldParseWithNoDataRows()
    {
        using var template = BuildTemplate(ImportTemplates.ErpCustomers);

        _customerReader.Read(template, 100).Should().BeEmpty();
    }

    [Fact]
    public void ProductTemplate_FilledWithThreeRows_ShouldParseEveryColumn()
    {
        // Cells are filled positionally under the template's own header order
        // (code, English name, Arabic name, points, price, category). Numbers are
        // typed as numbers, which is what Excel produces when a user fills the sheet
        // by hand — and the shape the reader normalizes.
        using var filled = FillTemplate(ImportTemplates.Products,
            ["P001", "LED Lamp", "مصباح LED", 10d, 25.50d, "إضاءة"],
            ["P002", "Cable", "كابل", 5d, 12d, "كهرباء"],
            ["P003", "Breaker", "قاطع", 7d, 0d, ""]);

        var rows = _productReader.Read(filled, 100);

        rows.Should().HaveCount(3);
        rows[0].Name.Should().Be("مصباح LED");
        rows[0].NameEn.Should().Be("LED Lamp");
        rows[0].ProductCode.Should().Be("P001");
        rows[0].Category.Should().Be("إضاءة");
        rows[0].PointValue.Should().Be("10");
        rows[0].Price.Should().Be("25.5");
        rows[2].Category.Should().BeEmpty();
        rows.Select(r => r.ProductCode).Should().Equal("P001", "P002", "P003");
    }

    [Fact]
    public void CustomerTemplate_FilledWithThreeRows_ShouldParseEveryColumn()
    {
        using var filled = FillTemplate(ImportTemplates.ErpCustomers,
            ["C001", "مؤسسة النور التجارية"],
            ["C002", "شركة الفجر"],
            [3003d, "مؤسسة الرياض"]);

        var rows = _customerReader.Read(filled, 100);

        rows.Should().HaveCount(3);
        rows[0].CustomerCode.Should().Be("C001");
        rows[0].CustomerName.Should().Be("مؤسسة النور التجارية");
        // A numeric code keeps all its digits rather than becoming "3003.0" or 3E+03.
        rows[2].CustomerCode.Should().Be("3003");
    }

    // Writes data rows into the template's first sheet exactly as a user filling the
    // downloaded file would — under the headers the template shipped with.
    private static MemoryStream FillTemplate(ImportTemplateDefinition definition, params object[][] rows)
    {
        using var template = BuildTemplate(definition);
        using var workbook = new XLWorkbook(template);
        var sheet = workbook.Worksheets.First();

        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                var cell = sheet.Cell(r + 2, c + 1);
                if (rows[r][c] is double number)
                    cell.Value = number;
                else
                    cell.Value = (string)rows[r][c];
            }
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    // Returns each key as its own value: enough for sheet names and the instruction
    // sheet's labels, none of which any parser reads.
    private sealed class EchoLocalizer : IStringLocalizer<ErrorMessages>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);

        public LocalizedString this[string name, params object[] arguments] =>
            new(name, string.Join(' ', arguments.Prepend(name)), resourceNotFound: false);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
