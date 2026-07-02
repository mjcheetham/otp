using System.Globalization;

namespace Mjcheetham.Otp;

/// <summary>
/// Parses otpauth:// "Key URI" values (as produced by authenticator apps and
/// QR codes) into <see cref="IOneTimePassword"/> instances.
/// </summary>
public static class OtpAuthUri
{
    public static IOneTimePassword Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException("An otpauth:// URI is required.");
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ||
            !string.Equals(uri.Scheme, "otpauth", StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException("Value is not a valid otpauth:// URI.");
        }

        OtpKind kind = uri.Host.ToLowerInvariant() switch
        {
            "totp" => OtpKind.TimeBased,
            "hotp" => OtpKind.Hmac,
            _ => throw new FormatException(
                $"Unknown one-time password type '{uri.Host}' in otpauth URI. Expected 'totp' or 'hotp'.")
        };

        // Label is "[issuer:]account". Split on the first literal colon before
        // unescaping so an encoded colon inside a component is not mistaken for
        // the separator.
        string rawLabel = uri.AbsolutePath.TrimStart('/');
        int colon = rawLabel.IndexOf(':');
        string? labelIssuer = colon >= 0 ? Uri.UnescapeDataString(rawLabel[..colon]).Trim() : null;
        string name = Uri.UnescapeDataString(colon >= 0 ? rawLabel[(colon + 1)..] : rawLabel).Trim();

        if (string.IsNullOrEmpty(name))
        {
            throw new FormatException("The otpauth URI does not contain an account name.");
        }

        Dictionary<string, string> query = ParseQuery(uri.Query);

        if (!query.TryGetValue("secret", out string? secretValue) || string.IsNullOrWhiteSpace(secretValue))
        {
            throw new FormatException("The otpauth URI is missing a 'secret' parameter.");
        }

        byte[] secret = Base32.Decode(secretValue);
        if (secret.Length == 0)
        {
            throw new FormatException("The otpauth URI 'secret' does not contain any data.");
        }

        int digits = ParseInt(query, "digits", 6);
        if (!OtpGenerator.TryValidateDigits(digits, out string? digitsError))
        {
            throw new FormatException(digitsError);
        }

        OtpAlgorithm algorithm = ParseAlgorithm(query.GetValueOrDefault("algorithm"));

        string? issuer = query.GetValueOrDefault("issuer");
        if (string.IsNullOrWhiteSpace(issuer))
        {
            issuer = labelIssuer;
        }

        if (kind == OtpKind.Hmac)
        {
            long counter = ParseLong(query, "counter", 0);
            if (!HmacOtp.TryValidateCounter(counter, out string? counterError))
            {
                throw new FormatException(counterError);
            }

            return new HmacOtp(name, secret, counter, digits, algorithm, issuer);
        }

        int period = ParseInt(query, "period", 30);
        if (!TimeBasedOtp.TryValidatePeriod(period, out string? periodError))
        {
            throw new FormatException(periodError);
        }

        return new TimeBasedOtp(name, secret, period, digits, algorithm, issuer);
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = pair.IndexOf('=');
            if (equals < 0)
            {
                continue;
            }

            string key = Uri.UnescapeDataString(pair[..equals]);
            string val = Uri.UnescapeDataString(pair[(equals + 1)..]);
            result[key] = val;
        }

        return result;
    }

    private static int ParseInt(Dictionary<string, string> query, string key, int fallback)
    {
        if (!query.TryGetValue(key, out string? raw))
        {
            return fallback;
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            throw new FormatException($"'{raw}' is not a valid value for '{key}' in the otpauth URI.");
        }

        return value;
    }

    private static long ParseLong(Dictionary<string, string> query, string key, long fallback)
    {
        if (!query.TryGetValue(key, out string? raw))
        {
            return fallback;
        }

        if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
        {
            throw new FormatException($"'{raw}' is not a valid value for '{key}' in the otpauth URI.");
        }

        return value;
    }

    private static OtpAlgorithm ParseAlgorithm(string? value) => value?.ToUpperInvariant() switch
    {
        null or "" or "SHA1" => OtpAlgorithm.Sha1,
        "SHA256" => OtpAlgorithm.Sha256,
        "SHA512" => OtpAlgorithm.Sha512,
        _ => throw new FormatException($"Unsupported algorithm '{value}' in the otpauth URI.")
    };
}
