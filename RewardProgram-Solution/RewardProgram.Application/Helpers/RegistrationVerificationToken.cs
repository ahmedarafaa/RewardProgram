using System.Security.Cryptography;
using System.Text;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Errors;

namespace RewardProgram.Application.Helpers;

public static class RegistrationVerificationToken
{
    public static string Generate(string mobile, string hmacKey, int expiryMinutes = 10)
    {
        var expiryUtc = DateTime.UtcNow.AddMinutes(expiryMinutes).Ticks.ToString();
        var payload = $"{mobile}|{expiryUtc}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var payloadBase64 = Convert.ToBase64String(payloadBytes);

        var signature = ComputeHmac(payloadBase64, hmacKey);
        return $"{payloadBase64}.{signature}";
    }

    public static Result<string> Validate(string token, string hmacKey)
    {
        var parts = token?.Split('.');
        if (parts == null || parts.Length != 2)
            return Result.Failure<string>(AuthErrors.VerificationTokenInvalid);

        var payloadBase64 = parts[0];
        var providedSignature = parts[1];

        var expectedSignature = ComputeHmac(payloadBase64, hmacKey);
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedSignature),
            Encoding.UTF8.GetBytes(providedSignature)))
            return Result.Failure<string>(AuthErrors.VerificationTokenInvalid);

        string payload;
        try
        {
            payload = Encoding.UTF8.GetString(Convert.FromBase64String(payloadBase64));
        }
        catch
        {
            return Result.Failure<string>(AuthErrors.VerificationTokenInvalid);
        }

        var segments = payload.Split('|');
        if (segments.Length != 2 || !long.TryParse(segments[1], out var expiryTicks))
            return Result.Failure<string>(AuthErrors.VerificationTokenInvalid);

        if (DateTime.UtcNow.Ticks > expiryTicks)
            return Result.Failure<string>(AuthErrors.VerificationTokenExpired);

        return Result.Success(segments[0]);
    }

    private static string ComputeHmac(string data, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash);
    }
}
