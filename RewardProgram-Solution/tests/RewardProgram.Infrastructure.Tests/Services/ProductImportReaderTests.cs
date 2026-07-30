using ClosedXML.Excel;
using FluentAssertions;
using RewardProgram.Infrastructure.Services;

namespace RewardProgram.Infrastructure.Tests.Services;

public class ProductImportReaderTests
{
    private readonly ProductImportReader _sut = new();

    // Builds a one-data-row workbook from a header row and a value row, so a test
    // states only the column layout it cares about.
    private static Stream Workbook(string[] headers, string[] values)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Sheet1");

        for (var i = 0; i < headers.Length; i++)
            sheet.Cell(1, i + 1).Value = headers[i];

        for (var i = 0; i < values.Length; i++)
            sheet.Cell(2, i + 1).Value = values[i];

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    [Theory]
    [InlineData("Name (EN)")]
    [InlineData("name en")]
    [InlineData("English Name")]
    [InlineData("الاسم بالإنجليزية")]
    public void Read_EnglishNameHeaderAlias_ShouldMapColumn(string header)
    {
        using var stream = Workbook(
            ["Name", header, "Product Code", "Point Value", "Price"],
            ["منتج", "LED BASE", "P001", "10", "5"]);

        var rows = _sut.Read(stream, 100);

        rows.Should().ContainSingle();
        rows[0].NameEn.Should().Be("LED BASE");
        rows[0].Name.Should().Be("منتج");
    }

    [Fact]
    public void Read_NoEnglishNameColumn_ShouldLeaveNameEnNull()
    {
        // Null (not empty) is what tells the service to leave a stored English name
        // alone — every product file uploaded before this feature has this shape.
        using var stream = Workbook(
            ["Name", "Product Code", "Point Value", "Price"],
            ["منتج", "P001", "10", "5"]);

        var rows = _sut.Read(stream, 100);

        rows.Should().ContainSingle();
        rows[0].NameEn.Should().BeNull();
    }

    [Fact]
    public void Read_EmptyEnglishNameCell_ShouldReturnEmptyNotNull()
    {
        using var stream = Workbook(
            ["Name", "Name (EN)", "Product Code", "Point Value", "Price"],
            ["منتج", "", "P001", "10", "5"]);

        var rows = _sut.Read(stream, 100);

        rows.Should().ContainSingle();
        rows[0].NameEn.Should().BeEmpty();
    }

    [Fact]
    public void Read_ProductNameHeader_ShouldStillMapToArabicName()
    {
        // "Product name" is the Arabic-name alias the importer has always used;
        // adding the English column must not re-point it.
        using var stream = Workbook(
            ["Product Name", "Product Code", "Point Value", "Price"],
            ["منتج", "P001", "10", "5"]);

        var rows = _sut.Read(stream, 100);

        rows.Should().ContainSingle();
        rows[0].Name.Should().Be("منتج");
        rows[0].NameEn.Should().BeNull();
    }

    [Fact]
    public void Read_RowBlankExceptEnglishName_ShouldNotBeSkipped()
    {
        // The row is still reported so the service can reject it with a per-row
        // error rather than the reader silently dropping it.
        using var stream = Workbook(
            ["Name", "Name (EN)", "Product Code", "Point Value", "Price"],
            ["", "Orphan English", "", "", ""]);

        var rows = _sut.Read(stream, 100);

        rows.Should().ContainSingle();
        rows[0].NameEn.Should().Be("Orphan English");
    }
}
