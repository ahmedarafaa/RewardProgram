using FluentAssertions;
using RewardProgram.Application.Helpers;

namespace RewardProgram.Application.Tests.Helpers;

public class MobileNumberHelperTests
{
    // ── Normalize ──

    [Fact]
    public void Normalize_05Format_ShouldConvertToPlus966()
    {
        MobileNumberHelper.Normalize("0512345678").Should().Be("+966512345678");
    }

    [Fact]
    public void Normalize_966WithoutPlus_ShouldAddPlus()
    {
        MobileNumberHelper.Normalize("966512345678").Should().Be("+966512345678");
    }

    [Fact]
    public void Normalize_Plus966Format_ShouldReturnAsIs()
    {
        MobileNumberHelper.Normalize("+966512345678").Should().Be("+966512345678");
    }

    [Fact]
    public void Normalize_WithWhitespace_ShouldTrim()
    {
        MobileNumberHelper.Normalize("  0512345678  ").Should().Be("+966512345678");
    }

    [Fact]
    public void Normalize_NullInput_ShouldReturnNull()
    {
        MobileNumberHelper.Normalize(null!).Should().BeNull();
    }

    [Fact]
    public void Normalize_EmptyString_ShouldReturnEmpty()
    {
        MobileNumberHelper.Normalize("").Should().BeEmpty();
    }

    [Fact]
    public void Normalize_WhitespaceOnly_ShouldReturnWhitespace()
    {
        MobileNumberHelper.Normalize("   ").Should().Be("   ");
    }

    [Fact]
    public void Normalize_NonSaudiNumber_ShouldReturnAsIs()
    {
        MobileNumberHelper.Normalize("+1234567890").Should().Be("+1234567890");
    }

    [Fact]
    public void Normalize_ShortNumberStartingWith05_ShouldNotConvert()
    {
        // Only 10-digit numbers starting with 05 should be converted
        MobileNumberHelper.Normalize("051234").Should().Be("051234");
    }

    // ── Mask ──

    [Fact]
    public void Mask_NormalNumber_ShouldShowFirst3AndLast3()
    {
        MobileNumberHelper.Mask("+966512345678").Should().Be("+96****678");
    }

    [Fact]
    public void Mask_NullInput_ShouldReturnStars()
    {
        MobileNumberHelper.Mask(null!).Should().Be("****");
    }

    [Fact]
    public void Mask_EmptyInput_ShouldReturnStars()
    {
        MobileNumberHelper.Mask("").Should().Be("****");
    }

    [Fact]
    public void Mask_ShortInput_ShouldReturnStars()
    {
        MobileNumberHelper.Mask("abc").Should().Be("****");
    }

    [Fact]
    public void Mask_ExactlyFourChars_ShouldMaskMiddle()
    {
        MobileNumberHelper.Mask("abcd").Should().Be("abc****bcd");
    }
}
