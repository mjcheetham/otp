using System.Text.Json.Serialization;

namespace Mjcheetham.Otp.Storage;

/// <summary>
/// Identifies which <see cref="IOtpStore"/> backend the application should use.
/// </summary>
public enum StoreBackend
{
    /// <summary>Use the current operating system's native store, falling back to
    /// <see cref="Plaintext"/> where none exists.</summary>
    [JsonStringEnumMemberName("auto")]
    Auto,

    /// <summary>The plaintext JSON file store (<see cref="FileOtpStore"/>).</summary>
    [JsonStringEnumMemberName("plaintext")]
    Plaintext,

    /// <summary>The macOS keychain (<see cref="MacOSKeychainOtpStore"/>).</summary>
    [JsonStringEnumMemberName("keychain")]
    Keychain,

    /// <summary>The Windows Credential Manager (<see cref="WindowsCredentialOtpStore"/>).</summary>
    [JsonStringEnumMemberName("wincred")]
    WinCred,

    /// <summary>The Linux Secret Service (<see cref="LinuxSecretServiceOtpStore"/>).</summary>
    [JsonStringEnumMemberName("secretservice")]
    SecretService
}

/// <summary>
/// Parses and formats the string identifiers used for <see cref="StoreBackend"/>
/// values in configuration, environment variables, and on the command line.
/// The identifiers match the JSON member names on <see cref="StoreBackend"/>.
/// </summary>
public static class StoreBackendParser
{
    private static readonly (StoreBackend Backend, string Id)[] Map =
    [
        (StoreBackend.Auto, "auto"),
        (StoreBackend.Plaintext, "plaintext"),
        (StoreBackend.Keychain, "keychain"),
        (StoreBackend.WinCred, "wincred"),
        (StoreBackend.SecretService, "secretservice")
    ];

    /// <summary>All backend identifiers, in declaration order.</summary>
    public static IReadOnlyList<string> Ids { get; } = Map.Select(m => m.Id).ToArray();

    /// <summary>Returns the string identifier for <paramref name="backend"/>.</summary>
    public static string ToId(this StoreBackend backend) =>
        Map.First(m => m.Backend == backend).Id;

    /// <summary>
    /// Parses a backend identifier (case-insensitively). Returns
    /// <see langword="false"/> when <paramref name="value"/> is not a known id.
    /// </summary>
    public static bool TryParse(string? value, out StoreBackend backend)
    {
        foreach ((StoreBackend candidate, string id) in Map)
        {
            if (string.Equals(id, value, StringComparison.OrdinalIgnoreCase))
            {
                backend = candidate;
                return true;
            }
        }

        backend = default;
        return false;
    }
}
