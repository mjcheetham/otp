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
}

public abstract class OneTimePassword(
    OtpKind kind,
    string name,
    byte[] secret,
    int digits,
    OtpAlgorithm algorithm,
    string? issuer = null) : IOneTimePassword
{
    public OtpKind Kind { get; } = kind;
    public string Name { get; } = name;
    public string? Issuer { get; } = issuer;
    internal byte[] Secret { get; } = secret;
    public int Digits { get; } = digits;
    public OtpAlgorithm Algorithm { get; } = algorithm;

    public abstract string GetCode();

    protected string GenerateCode(long counter) =>
        OtpGenerator.Generate(Secret, counter, Digits, Algorithm);
}
