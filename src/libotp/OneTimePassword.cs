namespace Mjcheetham.Otp;

public enum OtpKind
{
    TimeBased,
    Hmac
}

public interface IOneTimePassword
{
    OtpKind Kind { get; }

    string Name { get; }
}

public abstract class OneTimePassword(OtpKind kind, string name) : IOneTimePassword
{
    public OtpKind Kind { get; } = kind;
    public string Name { get; } = name;
}
