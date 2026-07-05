using Mjcheetham.Otp.Storage;

namespace Mjcheetham.Otp.Config;

/// <summary>
/// Resolves the effective <see cref="StoreBackend"/> and builds the
/// corresponding <see cref="IOtpStore"/>. Selection precedence is the
/// <c>OTP_STORE_BACKEND</c> environment variable, then the config file, then
/// <see cref="StoreBackend.Auto"/>.
/// </summary>
internal static class StoreResolver
{
    public static StoreBackend ResolveBackend(AppConfig config)
    {
        string? overrideValue = Environment.GetEnvironmentVariable("OTP_STORE_BACKEND");
        if (!string.IsNullOrEmpty(overrideValue))
        {
            if (!StoreBackendParser.TryParse(overrideValue, out StoreBackend backend))
            {
                throw new OtpStoreException(
                    $"OTP_STORE_BACKEND is set to '{overrideValue}', which is not a valid backend. " +
                    $"Valid values: {string.Join(", ", StoreBackendParser.Ids)}.");
            }

            return backend;
        }

        return config.Store.Backend ?? StoreBackend.Auto;
    }

    public static IOtpStore CreateStore(AppConfig config) =>
        OtpStoreFactory.Create(ResolveBackend(config));
}
