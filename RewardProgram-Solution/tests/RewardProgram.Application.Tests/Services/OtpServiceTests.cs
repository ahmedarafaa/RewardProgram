using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Interfaces.Auth;
using RewardProgram.Application.Services.Auth;
using RewardProgram.Application.Tests.TestHelpers;

namespace RewardProgram.Application.Tests.Services;

/// Covers the sync-error fallback path added alongside the Twilio status webhook
/// (commit 1992af5). When the WhatsApp Verify call returns a synchronous failure,
/// SendAsync should retry SMS in the same request rather than surfacing the
/// error to the user.
public class OtpServiceTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly ITwilioService _twilio;
    private readonly OtpService _sut;

    public OtpServiceTests()
    {
        _context = TestDbContext.Create();
        _twilio = Substitute.For<ITwilioService>();
        _sut = new OtpService(_twilio, _context, Substitute.For<ILogger<OtpService>>());
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task Send_WhatsAppSuccess_StoresRowOnWhatsAppChannel()
    {
        const string mobile = "+966500000001";
        _twilio.SendOtpAsync(Arg.Any<string>(), "whatsapp", Arg.Any<CancellationToken>())
            .Returns(Result.Success("VE_wa"));

        var result = await _sut.SendAsync(mobile);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("VE_wa");

        var row = await _context.OtpCodes.SingleAsync();
        row.PinId.Should().Be("VE_wa");
        row.CurrentSid.Should().Be("VE_wa");
        row.Channel.Should().Be("whatsapp");
        row.FallbackFired.Should().BeFalse();

        await _twilio.DidNotReceive().SendOtpAsync(Arg.Any<string>(), "sms", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_WhatsAppSyncError_RetriesSmsAndStoresRowOnSmsChannel()
    {
        const string mobile = "+966500000001";
        _twilio.SendOtpAsync(Arg.Any<string>(), "whatsapp", Arg.Any<CancellationToken>())
            .Returns(Result.Failure<string>(new Error("Twilio.Exception", "boom", 500)));
        _twilio.SendOtpAsync(Arg.Any<string>(), "sms", Arg.Any<CancellationToken>())
            .Returns(Result.Success("VE_sms"));

        var result = await _sut.SendAsync(mobile);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("VE_sms");

        var row = await _context.OtpCodes.SingleAsync();
        row.PinId.Should().Be("VE_sms");
        row.CurrentSid.Should().Be("VE_sms");
        row.Channel.Should().Be("sms");
        row.FallbackFired.Should().BeTrue(); // sync fallback counts as "fired"

        await _twilio.Received(1).SendOtpAsync(Arg.Any<string>(), "whatsapp", Arg.Any<CancellationToken>());
        await _twilio.Received(1).SendOtpAsync(Arg.Any<string>(), "sms", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_BothChannelsFail_SurfaceSmsErrorAndStoreNoRow()
    {
        const string mobile = "+966500000001";
        _twilio.SendOtpAsync(Arg.Any<string>(), "whatsapp", Arg.Any<CancellationToken>())
            .Returns(Result.Failure<string>(new Error("Twilio.Exception", "wa-boom", 500)));
        _twilio.SendOtpAsync(Arg.Any<string>(), "sms", Arg.Any<CancellationToken>())
            .Returns(Result.Failure<string>(new Error("Twilio.SmsException", "sms-boom", 500)));

        var result = await _sut.SendAsync(mobile);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Twilio.SmsException");

        (await _context.OtpCodes.CountAsync()).Should().Be(0); // no row created when both channels fail
    }

    // ── Resend: SMS-first ────────────────────────────────────────────────
    // User tapping Resend is a strong signal the prior WhatsApp didn't reach
    // them, so go straight to SMS rather than retrying the same channel.

    [Fact]
    public async Task Resend_PrefersSmsAndDoesNotCallWhatsApp()
    {
        const string mobile = "+966500000001";

        _context.OtpCodes.Add(new RewardProgram.Domain.Entities.OTP.OtpCode
        {
            PinId = "VE_old",
            CurrentSid = "VE_old",
            Channel = "whatsapp",
            MobileNumber = mobile,
            CreatedAt = DateTime.UtcNow.AddMinutes(-1), // past cooldown
            ExpiresAt = DateTime.UtcNow.AddMinutes(9),
            IsUsed = false
        });
        await _context.SaveChangesAsync();

        _twilio.SendOtpAsync(Arg.Any<string>(), "sms", Arg.Any<CancellationToken>())
            .Returns(Result.Success("VE_sms"));

        var result = await _sut.ResendAsync(mobile);

        result.IsSuccess.Should().BeTrue();
        result.Value.PinId.Should().Be("VE_sms");

        var newRow = await _context.OtpCodes
            .Where(o => o.PinId == "VE_sms")
            .SingleAsync();
        newRow.Channel.Should().Be("sms");
        newRow.FallbackFired.Should().BeFalse(); // SMS was primary

        var oldRow = await _context.OtpCodes
            .Where(o => o.PinId == "VE_old")
            .SingleAsync();
        oldRow.IsUsed.Should().BeTrue();

        await _twilio.DidNotReceive().SendOtpAsync(Arg.Any<string>(), "whatsapp", Arg.Any<CancellationToken>());
        await _twilio.Received(1).SendOtpAsync(Arg.Any<string>(), "sms", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resend_TwilioReturnsSameSid_UpdatesInPlaceWithoutDuplicate()
    {
        // Twilio Verify dedups by (Service, To): a second create while a
        // verification is still pending returns the SAME Sid. The row must
        // refresh in place instead of attempting a duplicate INSERT (which
        // would violate the unique PinId constraint and crash with 500).
        const string mobile = "+966500000001";
        const string sharedSid = "VE_dedup";

        _context.OtpCodes.Add(new RewardProgram.Domain.Entities.OTP.OtpCode
        {
            PinId = sharedSid,
            CurrentSid = sharedSid,
            Channel = "whatsapp",
            MobileNumber = mobile,
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            ExpiresAt = DateTime.UtcNow.AddMinutes(9),
            IsUsed = false,
            FallbackFired = false
        });
        await _context.SaveChangesAsync();

        _twilio.SendOtpAsync(Arg.Any<string>(), "sms", Arg.Any<CancellationToken>())
            .Returns(Result.Success(sharedSid)); // Twilio dedups → same Sid

        var result = await _sut.ResendAsync(mobile);

        result.IsSuccess.Should().BeTrue();
        result.Value.PinId.Should().Be(sharedSid);

        var rows = await _context.OtpCodes.ToListAsync();
        rows.Should().HaveCount(1);

        var row = rows[0];
        row.PinId.Should().Be(sharedSid);
        row.Channel.Should().Be("sms");      // refreshed to new channel
        row.IsUsed.Should().BeFalse();
        row.VerificationAttempts.Should().Be(0);
    }

    [Fact]
    public async Task Resend_CancelsPriorPendingVerificationBeforeSending()
    {
        const string mobile = "+966500000001";
        const string oldSid = "VE_old_whatsapp";

        _context.OtpCodes.Add(new RewardProgram.Domain.Entities.OTP.OtpCode
        {
            PinId = oldSid,
            CurrentSid = oldSid,
            Channel = "whatsapp",
            MobileNumber = mobile,
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            ExpiresAt = DateTime.UtcNow.AddMinutes(9),
            IsUsed = false
        });
        await _context.SaveChangesAsync();

        _twilio.CancelVerificationAsync(oldSid, Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _twilio.SendOtpAsync(Arg.Any<string>(), "sms", Arg.Any<CancellationToken>())
            .Returns(Result.Success("VE_new_sms"));

        var result = await _sut.ResendAsync(mobile);

        result.IsSuccess.Should().BeTrue();

        Received.InOrder(async () =>
        {
            await _twilio.CancelVerificationAsync(oldSid, Arg.Any<CancellationToken>());
            await _twilio.SendOtpAsync(Arg.Any<string>(), "sms", Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Resend_SmsSyncError_FallsBackToWhatsApp()
    {
        const string mobile = "+966500000001";

        _context.OtpCodes.Add(new RewardProgram.Domain.Entities.OTP.OtpCode
        {
            PinId = "VE_old",
            CurrentSid = "VE_old",
            Channel = "whatsapp",
            MobileNumber = mobile,
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            ExpiresAt = DateTime.UtcNow.AddMinutes(9),
            IsUsed = false
        });
        await _context.SaveChangesAsync();

        _twilio.SendOtpAsync(Arg.Any<string>(), "sms", Arg.Any<CancellationToken>())
            .Returns(Result.Failure<string>(new Error("Twilio.SmsException", "sms-boom", 500)));
        _twilio.SendOtpAsync(Arg.Any<string>(), "whatsapp", Arg.Any<CancellationToken>())
            .Returns(Result.Success("VE_wa"));

        var result = await _sut.ResendAsync(mobile);

        result.IsSuccess.Should().BeTrue();

        var newRow = await _context.OtpCodes
            .Where(o => o.PinId == "VE_wa")
            .SingleAsync();
        newRow.Channel.Should().Be("whatsapp");
        newRow.FallbackFired.Should().BeTrue();

        await _twilio.Received(1).SendOtpAsync(Arg.Any<string>(), "sms", Arg.Any<CancellationToken>());
        await _twilio.Received(1).SendOtpAsync(Arg.Any<string>(), "whatsapp", Arg.Any<CancellationToken>());
    }
}
