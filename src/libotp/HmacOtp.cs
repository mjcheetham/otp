namespace Mjcheetham.Otp;

public class HmacOtp(
    string name,
    byte[] secret,
    long counter = 0,
    int digits = 6,
    OtpAlgorithm algorithm = OtpAlgorithm.Sha1)
    : OneTimePassword(OtpKind.Hmac, name, secret, digits, algorithm)
{
    public long Counter { get; } = counter;

    public override string GetCode() => GenerateCode(Counter);

    public string GetCode(long counter) => GenerateCode(counter);
}
