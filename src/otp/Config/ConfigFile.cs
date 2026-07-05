using System.Text.Json;

namespace Mjcheetham.Otp.Config;

/// <summary>
/// Reads and writes <see cref="AppConfig"/> as JSON. The file lives at
/// <c>OTP_CONFIG</c> if set, otherwise <c>~/.otp/config.json</c>. Writes are
/// atomic and, on Unix, restricted to the owner.
/// </summary>
internal sealed class ConfigFile
{
    public ConfigFile(string filePath) => FilePath = filePath;

    public string FilePath { get; }

    public static string GetDefaultPath()
    {
        string? overridePath = Environment.GetEnvironmentVariable("OTP_CONFIG");
        if (!string.IsNullOrEmpty(overridePath))
        {
            return overridePath;
        }

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".otp", "config.json");
    }

    public AppConfig Load()
    {
        if (!File.Exists(FilePath))
        {
            return new AppConfig();
        }

        using FileStream stream = File.OpenRead(FilePath);
        return JsonSerializer.Deserialize(stream, AppConfigJsonContext.Default.AppConfig) ?? new AppConfig();
    }

    public void Save(AppConfig config)
    {
        string directory = Path.GetDirectoryName(FilePath)
            ?? throw new InvalidOperationException("The config path must include a directory.");
        Directory.CreateDirectory(directory);
        RestrictToOwner(directory, isDirectory: true);

        // Write to a sibling temp file then atomically move it into place so a
        // crash mid-write cannot corrupt an existing config.
        string tempPath = FilePath + ".tmp";
        using (FileStream stream = File.Create(tempPath))
        {
            JsonSerializer.Serialize(stream, config, AppConfigJsonContext.Default.AppConfig);
        }

        RestrictToOwner(tempPath, isDirectory: false);
        File.Move(tempPath, FilePath, overwrite: true);
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
}
