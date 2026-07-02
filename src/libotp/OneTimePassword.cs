using System.Text.Json.Serialization;

namespace Mjcheetham.Otp;

public enum OtpKind
{
    [JsonStringEnumMemberName("totp")]
    TimeBased,
    [JsonStringEnumMemberName("hotp")]
    Hmac
}

public interface IOneTimePassword
{
    OtpKind Kind { get; }

    string Name { get; }

    string? Issuer { get; }

    int Digits { get; }

    OtpAlgorithm Algorithm { get; }

    string GetCode();

    string GetSecret();
}

public abstract class OneTimePassword : IOneTimePassword
{
    protected OneTimePassword(
        OtpKind kind,
        string name,
        byte[] secret,
        int digits,
        OtpAlgorithm algorithm,
        string? issuer = null)
    {
        ArgumentNullException.ThrowIfNull(secret);
        if (secret.Length == 0)
        {
            throw new ArgumentException("A secret is required.", nameof(secret));
        }

        if (!OtpGenerator.TryValidateDigits(digits, out string? error))
        {
            throw new ArgumentOutOfRangeException(nameof(digits), digits, error);
        }

        Kind = kind;
        Name = name;
        Secret = secret;
        Digits = digits;
        Algorithm = algorithm;
        Issuer = issuer;
    }

    public OtpKind Kind { get; }
    public string Name { get; }
    public string? Issuer { get; }
    internal byte[] Secret { get; }
    public int Digits { get; }
    public OtpAlgorithm Algorithm { get; }

    public abstract string GetCode();

    public string GetSecret() => Base32.Encode(Secret);

    protected string GenerateCode(long counter) =>
        OtpGenerator.Generate(Secret, counter, Digits, Algorithm);
}
