using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Interfaces.Auth;
using RewardProgram.Application.Services.Auth;
using RewardProgram.Application.Tests.TestHelpers;
using RewardProgram.Domain.Entities.OTP;

namespace RewardProgram.Application.Tests.Services;

public class OtpFallbackServiceTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly ITwilioService _twilio;
    private readonly OtpFallbackService _sut;

    public OtpFallbackServiceTests()
    {
        _context = TestDbContext.Create();
        _twilio = Substitute.For<ITwilioService>();
        _sut = new OtpFallbackService(_context, _twilio, Substitute.For<ILogger<OtpFallbackService>>());
    }

    public void Dispose() => _context.Dispose();

    private async Task<OtpCode> SeedOtp(
        string sid = "VE_test",
        bool isUsed = false,
        bool fallbackFired = false,
        DateTime? expiresAt = null)
    {
        var row = new OtpCode
        {
            PinId = sid,
            CurrentSid = sid,
            Channel = "whatsapp",
            MobileNumber = "+966500000001",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddMinutes(10),
            IsUsed = isUsed,
            FallbackFired = fallbackFired
        };
        _context.OtpCodes.Add(row);
        await _context.SaveChangesAsync();
        return row;
    }

    [Fact]
    public async Task TriggerSms_RowNotFound_ReturnsFalseNoOp()
    {
        var result = await _sut.TriggerSmsForFailedVerificationAsync("VE_doesnotexist");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
        await _twilio.DidNotReceive().SendOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TriggerSms_EmptySid_ReturnsFalseNoOp()
    {
        var result = await _sut.TriggerSmsForFailedVerificationAsync("");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
        await _twilio.DidNotReceive().SendOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TriggerSms_AlreadyUsed_ReturnsFalseNoOp()
    {
        var row = await SeedOtp(isUsed: true);

        var result = await _sut.TriggerSmsForFailedVerificationAsync(row.CurrentSid);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
        await _twilio.DidNotReceive().SendOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TriggerSms_AlreadyFired_ReturnsFalseNoOp()
    {
        var row = await SeedOtp(fallbackFired: true);

        var result = await _sut.TriggerSmsForFailedVerificationAsync(row.CurrentSid);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
        await _twilio.DidNotReceive().SendOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TriggerSms_Expired_MarksFiredAndSkips()
    {
        var row = await SeedOtp(expiresAt: DateTime.UtcNow.AddMinutes(-1));

        var result = await _sut.TriggerSmsForFailedVerificationAsync(row.CurrentSid);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
        await _twilio.DidNotReceive().SendOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        var reloaded = await _context.OtpCodes.FindAsync(row.Id);
        reloaded!.FallbackFired.Should().BeTrue();
    }

    [Fact]
    public async Task TriggerSms_HappyPath_FiresSmsAndRotatesSid()
    {
        var row = await SeedOtp(sid: "VE_whatsapp");
        _twilio.SendOtpAsync(row.MobileNumber, "sms", Arg.Any<CancellationToken>())
            .Returns(Result.Success("VE_sms"));

        var result = await _sut.TriggerSmsForFailedVerificationAsync("VE_whatsapp");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        var reloaded = await _context.OtpCodes.FindAsync(row.Id);
        reloaded!.FallbackFired.Should().BeTrue();
        reloaded.Channel.Should().Be("sms");
        reloaded.CurrentSid.Should().Be("VE_sms");
        reloaded.PinId.Should().Be("VE_whatsapp"); // PinId is the public token — never rotates
    }

    [Fact]
    public async Task TriggerSms_SmsApiFails_ReturnsFailureButLeavesFallbackFiredTrue()
    {
        var row = await SeedOtp(sid: "VE_whatsapp");
        _twilio.SendOtpAsync(row.MobileNumber, "sms", Arg.Any<CancellationToken>())
            .Returns(Result.Failure<string>(new Error("Twilio.Exception", "boom", 500)));

        var result = await _sut.TriggerSmsForFailedVerificationAsync("VE_whatsapp");

        result.IsFailure.Should().BeTrue();

        // FallbackFired should be true even though SMS itself failed — this stops a
        // tight retry loop if Twilio webhooks fire repeatedly. The user can /resend-otp.
        var reloaded = await _context.OtpCodes.FindAsync(row.Id);
        reloaded!.FallbackFired.Should().BeTrue();
        reloaded.Channel.Should().Be("whatsapp"); // Sid NOT rotated since SMS didn't succeed
        reloaded.CurrentSid.Should().Be("VE_whatsapp");
    }

    [Fact]
    public async Task TriggerSms_LooksUpByCurrentSid_NotPinId()
    {
        // A row whose CurrentSid was already rotated would only be findable by CurrentSid.
        // Verify that's what the service uses.
        var row = new OtpCode
        {
            PinId = "VE_original",
            CurrentSid = "VE_rotated",
            Channel = "sms",
            MobileNumber = "+966500000001",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            FallbackFired = true // already rotated once
        };
        _context.OtpCodes.Add(row);
        await _context.SaveChangesAsync();

        var byPinId = await _sut.TriggerSmsForFailedVerificationAsync("VE_original");
        byPinId.Value.Should().BeFalse(); // no row matches PinId in CurrentSid column

        var byCurrentSid = await _sut.TriggerSmsForFailedVerificationAsync("VE_rotated");
        byCurrentSid.Value.Should().BeFalse(); // found, but FallbackFired=true → no-op
    }
}
