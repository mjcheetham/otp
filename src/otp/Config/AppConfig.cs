using System.Text.Json.Serialization;
using Mjcheetham.Otp.Storage;

namespace Mjcheetham.Otp.Config;

/// <summary>
/// The persisted application configuration. This is a command-line concern: the
/// library exposes the stores as reusable types, while the CLI decides which one
/// to use based on this file (and environment overrides).
/// </summary>
internal sealed class AppConfig
{
    public StoreSection Store { get; set; } = new();
}

internal sealed class StoreSection
{
    /// <summary>The configured backend, or <see langword="null"/> when unset
    /// (which resolves to <see cref="StoreBackend.Auto"/>).</summary>
    public StoreBackend? Backend { get; set; }
}

[JsonSerializable(typeof(AppConfig))]
[JsonSourceGenerationOptions(
    WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, UseStringEnumConverter = true)]
internal partial class AppConfigJsonContext : JsonSerializerContext;
