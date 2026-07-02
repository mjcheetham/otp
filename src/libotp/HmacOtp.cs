using System.Diagnostics.CodeAnalysis;

namespace Mjcheetham.Otp;

public class HmacOtp : OneTimePassword
{
    public HmacOtp(
        string name,
        byte[] secret,
        long counter = 0,
        int digits = 6,
        OtpAlgorithm algorithm = OtpAlgorithm.Sha1,
        string? issuer = null)
        : base(OtpKind.Hmac, name, secret, digits, algorithm, issuer)
    {
        if (!TryValidateCounter(counter, out string? error))
        {
            throw new ArgumentOutOfRangeException(nameof(counter), counter, error);
        }

        Counter = counter;
    }

    public long Counter { get; }

    /// <summary>
    /// Validates that <paramref name="counter"/> is not negative. Returns
    /// <see langword="false"/> and a human-readable <paramref name="error"/>
    /// message when it is.
    /// </summary>
    public static bool TryValidateCounter(long counter, [NotNullWhen(false)] out string? error)
    {
        if (counter < 0)
        {
            error = "The counter cannot be negative.";
            return false;
        }

        error = null;
        return true;
    }

    public override string GetCode() => GenerateCode(Counter);

    public string GetCode(long counter) => GenerateCode(counter);
}
