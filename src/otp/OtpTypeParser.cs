using System.CommandLine.Parsing;

namespace Mjcheetham.Otp;

internal static class OtpTypeParser
{
    public static bool TryParse(string? token, out OtpKind kind)
    {
        switch (token?.Trim().ToLowerInvariant())
        {
            case "totp":
            case "timebased":
                kind = OtpKind.TimeBased;
                return true;

            case "hotp":
            case "hmac":
                kind = OtpKind.Hmac;
                return true;

            default:
                kind = default;
                return false;
        }
    }

    public static OtpKind Parse(ArgumentResult result)
    {
        if (result.Tokens.Count == 0)
        {
            return OtpKind.TimeBased;
        }

        string token = result.Tokens[0].Value;
        if (TryParse(token, out OtpKind kind))
        {
            return kind;
        }

        result.AddError($"'{token}' is not a valid OTP type. Expected 'totp' or 'hotp'.");
        return default;
    }

    public static OtpKind? ParseNullable(ArgumentResult result)
    {
        if (result.Tokens.Count == 0)
        {
            return null;
        }

        string token = result.Tokens[0].Value;
        if (TryParse(token, out OtpKind kind))
        {
            return kind;
        }

        result.AddError($"'{token}' is not a valid OTP type. Expected 'totp' or 'hotp'.");
        return null;
    }
}
