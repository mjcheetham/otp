using Mjcheetham.Otp.Storage;

namespace Mjcheetham.Otp;

/// <summary>
/// Thrown when an <see cref="IOtpStore"/> backend fails to complete an
/// operation - for example when an operating-system secret vault is locked,
/// unavailable, or returns an unexpected error.
/// </summary>
public class OtpStoreException : Exception
{
    public OtpStoreException(string message) : base(message)
    {
    }

    public OtpStoreException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
