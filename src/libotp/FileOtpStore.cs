using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mjcheetham.Otp;

/// <summary>
/// An <see cref="IOtpStore"/> backed by a plaintext JSON file. Secrets are
/// stored unencrypted; the containing directory and file are restricted to the
/// owner. The backend is intended to be swappable (e.g. an OS keychain or an
/// encrypted file) behind <see cref="IOtpStore"/> in the future.
/// </summary>
public sealed partial class FileOtpStore : IOtpStore
{
    [JsonSerializable(typeof(StoreFile))]
    [JsonSourceGenerationOptions(
        WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, UseStringEnumConverter = true)]
    internal partial class FileOtpStoreJsonContext : JsonSerializerContext;

    private const int CurrentVersion = 1;

    private readonly string _path;
    private List<IOneTimePassword>? _cache;

    public FileOtpStore(string path)
    {
        _path = path;
    }

    /// <summary>
    /// Resolves the default store path: the value of the <c>OTP_STORE</c>
    /// environment variable if set, otherwise <c>~/.otp/store.json</c>.
    /// </summary>
    public static string GetDefaultPath()
    {
        string? overridePath = Environment.GetEnvironmentVariable("OTP_STORE");
        if (!string.IsNullOrEmpty(overridePath))
        {
            return overridePath;
        }

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".otp", "store.json");
    }

    public async IAsyncEnumerable<IOneTimePassword> ListAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        List<IOneTimePassword> items = await EnsureLoadedAsync(ct);
        foreach (IOneTimePassword otp in items)
        {
            yield return otp;
        }
    }

    public async ValueTask<IOneTimePassword?> GetAsync(string name, CancellationToken ct = default)
    {
        List<IOneTimePassword> items = await EnsureLoadedAsync(ct);
        return items.Find(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task AddAsync(IOneTimePassword otp, CancellationToken ct = default)
    {
        List<IOneTimePassword> items = await EnsureLoadedAsync(ct);
        if (items.Any(o => string.Equals(o.Name, otp.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"A one-time password named '{otp.Name}' already exists.");
        }

        items.Add(otp);
        await SaveAsync(items, ct);
    }

    public async Task<bool> RemoveAsync(string name, CancellationToken ct = default)
    {
        List<IOneTimePassword> items = await EnsureLoadedAsync(ct);
        int removed = items.RemoveAll(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));
        if (removed == 0)
        {
            return false;
        }

        await SaveAsync(items, ct);
        return true;
    }

    private async Task<List<IOneTimePassword>> EnsureLoadedAsync(CancellationToken ct)
    {
        if (_cache is not null)
        {
            return _cache;
        }

        if (!File.Exists(_path))
        {
            _cache = new List<IOneTimePassword>();
            return _cache;
        }

        StoreFile? file;
        await using (FileStream stream = File.OpenRead(_path))
        {
            file = await JsonSerializer.DeserializeAsync(stream, FileOtpStoreJsonContext.Default.StoreFile, ct);
        }

        var items = new List<IOneTimePassword>();
        foreach (OtpEntry entry in file?.Entries ?? [])
        {
            items.Add(entry.ToOtp());
        }

        _cache = items;
        return _cache;
    }

    private async Task SaveAsync(List<IOneTimePassword> items, CancellationToken ct)
    {
        string directory = Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException("The store path must include a directory.");
        Directory.CreateDirectory(directory);
        RestrictToOwner(directory, isDirectory: true);

        var file = new StoreFile
        {
            Version = CurrentVersion,
            Entries = items.Select(OtpEntry.FromOtp).ToList()
        };

        // Write to a sibling temp file then atomically move it into place so a
        // crash mid-write cannot corrupt an existing store.
        string tempPath = _path + ".tmp";
        await using (FileStream stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, file, FileOtpStoreJsonContext.Default.StoreFile, ct);
        }

        RestrictToOwner(tempPath, isDirectory: false);
        File.Move(tempPath, _path, overwrite: true);

        _cache = items;
    }

    private static void RestrictToOwner(string path, bool isDirectory)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        UnixFileMode mode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        if (isDirectory)
        {
            mode |= UnixFileMode.UserExecute;
        }

        File.SetUnixFileMode(path, mode);
    }

    internal sealed class StoreFile
    {
        public int Version { get; set; }

        public List<OtpEntry> Entries { get; set; } = [];
    }

    internal sealed class OtpEntry
    {
        public string Name { get; set; } = string.Empty;

        public OtpKind Kind { get; set; }

        public string Secret { get; set; } = string.Empty;

        public int Digits { get; set; }

        public OtpAlgorithm Algorithm { get; set; }

        public int? Period { get; set; }

        public long? Counter { get; set; }

        public static OtpEntry FromOtp(IOneTimePassword otp)
        {
            var entry = new OtpEntry
            {
                Name = otp.Name,
                Kind = otp.Kind,
                Digits = otp.Digits,
                Algorithm = otp.Algorithm
            };

            switch (otp)
            {
                case TimeBasedOtp totp:
                    entry.Secret = Base32.Encode(totp.Secret);
                    entry.Period = totp.Period;
                    break;

                case HmacOtp hotp:
                    entry.Secret = Base32.Encode(hotp.Secret);
                    entry.Counter = hotp.Counter;
                    break;

                default:
                    throw new NotSupportedException(
                        $"Cannot persist one-time password of type '{otp.GetType().Name}'.");
            }

            return entry;
        }

        public IOneTimePassword ToOtp()
        {
            byte[] secret = Base32.Decode(Secret);
            return Kind switch
            {
                OtpKind.TimeBased => new TimeBasedOtp(Name, secret, Period ?? 30, Digits, Algorithm),
                OtpKind.Hmac => new HmacOtp(Name, secret, Counter ?? 0, Digits, Algorithm),
                _ => throw new NotSupportedException($"Unknown one-time password kind '{Kind}'.")
            };
        }
    }
}
