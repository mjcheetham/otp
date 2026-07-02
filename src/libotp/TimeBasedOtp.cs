using System.Diagnostics.CodeAnalysis;

namespace Mjcheetham.Otp;

public class TimeBasedOtp : OneTimePassword
{
    public TimeBasedOtp(
        string name,
        byte[] secret,
        int period = 30,
        int digits = 6,
        OtpAlgorithm algorithm = OtpAlgorithm.Sha1,
        string? issuer = null)
        : base(OtpKind.TimeBased, name, secret, digits, algorithm, issuer)
    {
        if (!TryValidatePeriod(period, out string? error))
        {
            throw new ArgumentOutOfRangeException(nameof(period), period, error);
        }

        Period = period;
    }

    public int Period { get; }

    /// <summary>
    /// Validates that <paramref name="period"/> is a positive number of seconds.
    /// Returns <see langword="false"/> and a human-readable <paramref name="error"/>
    /// message when it is not.
    /// </summary>
    public static bool TryValidatePeriod(int period, [NotNullWhen(false)] out string? error)
    {
        if (period <= 0)
        {
            error = "The period must be greater than zero.";
            return false;
        }

        error = null;
        return true;
    }

    public override string GetCode() => GetCode(DateTimeOffset.UtcNow);

    public string GetCode(DateTimeOffset time) =>
        GenerateCode(time.ToUnixTimeSeconds() / Period);
}
