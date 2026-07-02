using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;

namespace Mjcheetham.Otp;

public static class OtpGenerator
{
    public static string Generate(
        ReadOnlySpan<byte> secret,
        long counter,
        int digits,
        OtpAlgorithm algorithm)
    {
        if (digits is < 1 or > 9)
        {
            throw new ArgumentOutOfRangeException(
                nameof(digits), digits, "Digits must be between 1 and 9.");
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
