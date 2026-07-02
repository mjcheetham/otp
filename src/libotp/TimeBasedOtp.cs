namespace Mjcheetham.Otp;

public class TimeBasedOtp(
    string name,
    byte[] secret,
    int period = 30,
    int digits = 6,
    OtpAlgorithm algorithm = OtpAlgorithm.Sha1)
    : OneTimePassword(OtpKind.TimeBased, name, secret, digits, algorithm)
{
    public int Period { get; } = period;

    public override string GetCode() => GetCode(DateTimeOffset.UtcNow);

    public string GetCode(DateTimeOffset time) =>
        GenerateCode(time.ToUnixTimeSeconds() / Period);
}
