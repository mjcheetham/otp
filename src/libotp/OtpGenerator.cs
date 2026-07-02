using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;

namespace Mjcheetham.Otp;

public static class OtpGenerator
{
    /// <summary>
    /// Validates that <paramref name="digits"/> is within the supported range
    /// (1–9). Returns <see langword="false"/> and a human-readable
    /// <paramref name="error"/> message when it is not.
    /// </summary>
    public static bool TryValidateDigits(int digits, [NotNullWhen(false)] out string? error)
    {
        if (digits is < 1 or > 9)
        {
            error = "Digits must be between 1 and 9.";
            return false;
        }

        error = null;
        return true;
    }

    public static string Generate(
        ReadOnlySpan<byte> secret,
        long counter,
        int digits,
        OtpAlgorithm algorithm)
    {
        if (!TryValidateDigits(digits, out string? error))
        {
            throw new ArgumentOutOfRangeException(nameof(digits), digits, error);
        }

        Span<byte> message = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(message, counter);

        // Largest supported digest (SHA-512) is 64 bytes.
        Span<byte> hash = stackalloc byte[64];
        int hashLength = algorithm switch
        {
            OtpAlgorithm.Sha1 => HMACSHA1.HashData(secret, message, hash),
            OtpAlgorithm.Sha256 => HMACSHA256.HashData(secret, message, hash),
            OtpAlgorithm.Sha512 => HMACSHA512.HashData(secret, message, hash),
            _ => throw new ArgumentOutOfRangeException(
                nameof(algorithm), algorithm, "Unsupported OTP algorithm.")
        };
        hash = hash[..hashLength];

        // RFC 4226 dynamic truncation.
        int offset = hash[^1] & 0x0f;
        int binary =
            ((hash[offset] & 0x7f) << 24) |
            ((hash[offset + 1] & 0xff) << 16) |
            ((hash[offset + 2] & 0xff) << 8) |
            (hash[offset + 3] & 0xff);

        int code = binary % (int)Math.Pow(10, digits);
        return code.ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0');
    }
}
