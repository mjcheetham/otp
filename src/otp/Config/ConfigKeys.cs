using Mjcheetham.Otp.Storage;

namespace Mjcheetham.Otp.Config;

/// <summary>
/// A single configuration key, exposing typed access to its value on an
/// <see cref="AppConfig"/> so the <c>config</c> command can stay generic.
/// </summary>
internal sealed class ConfigKey
{
    /// <summary>The dotted key name, e.g. <c>store.backend</c>.</summary>
    public required string Name { get; init; }

    /// <summary>The explicitly-set value, or <see langword="null"/> when unset.</summary>
    public required Func<AppConfig, string?> GetRaw { get; init; }

    /// <summary>The effective value, with the default applied when unset.</summary>
    public required Func<AppConfig, string> GetEffective { get; init; }

    /// <summary>Parses, validates, and applies <paramref name="value"/>; throws
    /// <see cref="FormatException"/> when it is not valid for this key.</summary>
    public required Action<AppConfig, string> Set { get; init; }

    /// <summary>Removes the key, reverting it to its default.</summary>
    public required Action<AppConfig> Unset { get; init; }
}

/// <summary>
/// The registry of known configuration keys. Add a <see cref="ConfigKey"/> here
/// to expose a new setting to the <c>config</c> command.
/// </summary>
internal static class ConfigKeys
{
    private static readonly ConfigKey StoreBackendKey = new()
    {
        Name = "store.backend",
        GetRaw = config => config.Store.Backend?.ToId(),
        GetEffective = config => (config.Store.Backend ?? StoreBackend.Auto).ToId(),
        Set = (config, value) =>
        {
            if (!StoreBackendParser.TryParse(value, out StoreBackend backend))
            {
                throw new FormatException(
                    $"'{value}' is not a valid backend. Valid values: {string.Join(", ", StoreBackendParser.Ids)}.");
            }

            config.Store.Backend = backend;
        },
        Unset = config => config.Store.Backend = null
    };

    public static IReadOnlyList<ConfigKey> All { get; } = [StoreBackendKey];

    public static ConfigKey? Find(string name) =>
        All.FirstOrDefault(key => string.Equals(key.Name, name, StringComparison.OrdinalIgnoreCase));
}
