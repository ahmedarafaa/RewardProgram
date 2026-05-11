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
}
