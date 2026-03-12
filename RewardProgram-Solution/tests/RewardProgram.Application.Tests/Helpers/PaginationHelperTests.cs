using FluentAssertions;
using RewardProgram.Application.Helpers;

namespace RewardProgram.Application.Tests.Helpers;

public class PaginationHelperTests
{
    [Fact]
    public void Normalize_ValidValues_ShouldReturnAsIs()
    {
        var (page, pageSize) = PaginationHelper.Normalize(2, 20);

        page.Should().Be(2);
        pageSize.Should().Be(20);
    }

    [Fact]
    public void Normalize_ZeroPage_ShouldClampToOne()
    {
        var (page, _) = PaginationHelper.Normalize(0, 20);

        page.Should().Be(1);
    }

    [Fact]
    public void Normalize_NegativePage_ShouldClampToOne()
    {
        var (page, _) = PaginationHelper.Normalize(-5, 20);

        page.Should().Be(1);
    }

    [Fact]
    public void Normalize_ZeroPageSize_ShouldClampToOne()
    {
        var (_, pageSize) = PaginationHelper.Normalize(1, 0);

        pageSize.Should().Be(1);
    }

    [Fact]
    public void Normalize_NegativePageSize_ShouldClampToOne()
    {
        var (_, pageSize) = PaginationHelper.Normalize(1, -10);

        pageSize.Should().Be(1);
    }

    [Fact]
    public void Normalize_PageSizeExceedsMax_ShouldClampToMax()
    {
        var (_, pageSize) = PaginationHelper.Normalize(1, 200);

        pageSize.Should().Be(100); // default max is 100
    }

    [Fact]
    public void Normalize_CustomMaxPageSize_ShouldClampToCustomMax()
    {
        var (_, pageSize) = PaginationHelper.Normalize(1, 50, maxPageSize: 25);

        pageSize.Should().Be(25);
    }

    [Fact]
    public void Normalize_PageSizeAtMax_ShouldReturnMax()
    {
        var (_, pageSize) = PaginationHelper.Normalize(1, 100);

        pageSize.Should().Be(100);
    }
}
