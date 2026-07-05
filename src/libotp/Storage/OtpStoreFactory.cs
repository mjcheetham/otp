namespace Mjcheetham.Otp.Storage;

/// <summary>
/// Creates the <see cref="IOtpStore"/> for a requested <see cref="StoreBackend"/>,
/// resolving <see cref="StoreBackend.Auto"/> to the current operating system's
/// native store.
/// </summary>
public static class OtpStoreFactory
{
    /// <summary>
    /// Creates the store for <paramref name="backend"/>. Throws
    /// <see cref="OtpStoreException"/> if a platform-specific backend is
    /// requested on a different operating system.
    /// </summary>
    public static IOtpStore Create(StoreBackend backend) => backend switch
    {
        StoreBackend.Auto => CreateNative(),
        StoreBackend.Plaintext => CreatePlaintext(),
        StoreBackend.Keychain or StoreBackend.WinCred or StoreBackend.SecretService
            => CreatePlatform(backend),
        _ => throw new ArgumentOutOfRangeException(nameof(backend))
    };

    private static IOtpStore CreateNative()
    {
        if (OperatingSystem.IsMacOS())
        {
            return new MacOSKeychainOtpStore();
        }

        if (OperatingSystem.IsWindows())
        {
            return new WindowsCredentialOtpStore();
        }

        if (OperatingSystem.IsLinux())
        {
            return new LinuxSecretServiceOtpStore();
        }

        // No native store for this OS; fall back to the plaintext file store.
        return CreatePlaintext();
    }

    private static IOtpStore CreatePlatform(StoreBackend backend) => backend switch
    {
        StoreBackend.Keychain when OperatingSystem.IsMacOS() => new MacOSKeychainOtpStore(),
        StoreBackend.WinCred when OperatingSystem.IsWindows() => new WindowsCredentialOtpStore(),
        StoreBackend.SecretService when OperatingSystem.IsLinux() => new LinuxSecretServiceOtpStore(),
        _ => throw new OtpStoreException(
            $"the '{backend.ToId()}' backend is only available on {PlatformName(backend)}; " +
            "use 'auto' or 'plaintext'.")
    };

    private static IOtpStore CreatePlaintext() => new FileOtpStore(FileOtpStore.GetDefaultPath());

    private static string PlatformName(StoreBackend backend) => backend switch
    {
        StoreBackend.Keychain => "macOS",
        StoreBackend.WinCred => "Windows",
        StoreBackend.SecretService => "Linux",
        _ => "this platform"
    };
}
