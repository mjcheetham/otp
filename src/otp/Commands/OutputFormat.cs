using System.CommandLine;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Mjcheetham.Otp.Commands;

internal enum OutputFormat
{
    Text,
    Json,
    Nul
}

/// <summary>
/// Shared <c>--format</c>/<c>-z</c> options for commands that emit data, letting
/// the caller choose human-readable text, JSON, or NUL-delimited output.
/// </summary>
internal sealed class FormatOptions
{
    public Option<OutputFormat> Format { get; } = new("--format", "-f")
    {
        Description = "Output format: text (human-readable), json, or nul (NUL-delimited, script-friendly).",
        HelpName = "text|json|nul"
    };

    public Option<bool> Nul { get; } = new("-z")
    {
        Description = "Shorthand for --format nul (NUL-delimited, git -z style)."
    };

    public void AddTo(Command command)
    {
        command.Add(Format);
        command.Add(Nul);
    }

    public OutputFormat Resolve(ParseResult result) =>
        result.GetValue(Nul) ? OutputFormat.Nul : result.GetValue(Format);
}

internal static class OtpFormat
{
    public static string TypeMachine(OtpKind kind) => kind == OtpKind.Hmac ? "hotp" : "totp";

    public static string TypeHuman(OtpKind kind) =>
        kind == OtpKind.Hmac ? "Counter-based (HOTP)" : "Time-based (TOTP)";

    public static string AlgorithmMachine(OtpAlgorithm algorithm) =>
        algorithm.ToString().ToLowerInvariant();

    public static string AlgorithmHuman(OtpAlgorithm algorithm) => algorithm switch
    {
        OtpAlgorithm.Sha1 => "SHA-1",
        OtpAlgorithm.Sha256 => "SHA-256",
        OtpAlgorithm.Sha512 => "SHA-512",
        _ => algorithm.ToString()
    };

    public static string IsoUtc(DateTimeOffset time) =>
        time.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>
    /// Builds a compact JSON string with <see cref="Utf8JsonWriter"/>, which is
    /// reflection-free and therefore safe under trimming and native AOT.
    /// </summary>
    public static string Json(Action<Utf8JsonWriter> write)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            write(writer);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}

/// <summary>
/// Accumulates NUL-delimited output. Key/value records are written as
/// <c>key&lt;LF&gt;value&lt;NUL&gt;</c> (git <c>config -z</c> style); flat list
/// items are written as <c>value&lt;NUL&gt;</c>.
/// </summary>
internal sealed class NulWriter
{
    private readonly StringBuilder _builder = new();

    public NulWriter Field(string key, string value)
    {
        _builder.Append(key).Append('\n').Append(value).Append('\0');
        return this;
    }

    public NulWriter Item(string value)
    {
        _builder.Append(value).Append('\0');
        return this;
    }

    public override string ToString() => _builder.ToString();
}
