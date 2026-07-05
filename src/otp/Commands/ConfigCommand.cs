using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
using Mjcheetham.Otp.Config;
using Mjcheetham.Otp.Storage;
using Spectre.Console;

namespace Mjcheetham.Otp.Commands;

internal sealed class ConfigCommand : Command
{
    public ConfigCommand(ConfigFile configFile) : base("config", "View and edit configuration.")
    {
        Add(new ConfigGetCommand(configFile));
        Add(new ConfigSetCommand(configFile));
        Add(new ConfigUnsetCommand(configFile));
        Add(new ConfigListCommand(configFile));
        Add(new ConfigEditCommand(configFile));
    }
}

internal sealed class ConfigGetCommand : Command
{
    private readonly ConfigFile _configFile;
    private readonly FormatOptions _format = new();

    private readonly Argument<string> _keyArg = new("key")
    {
        Description = "Configuration key to read (e.g. store.backend)."
    };

    public ConfigGetCommand(ConfigFile configFile)
        : base("get", "Print the effective value of a configuration key.")
    {
        _configFile = configFile;
        Add(_keyArg);
        _format.AddTo(this);
        SetAction(Execute);
    }

    private int Execute(ParseResult result)
    {
        string key = result.GetRequiredValue(_keyArg);
        if (ConfigKeys.Find(key) is not { } configKey)
        {
            Ui.ReportError(ConfigCommandHelpers.UnknownKey(key));
            return 1;
        }

        string value = configKey.GetEffective(_configFile.Load());

        switch (_format.Resolve(result))
        {
            case OutputFormat.Json:
                Console.WriteLine(OtpFormat.Json(writer =>
                {
                    writer.WriteStartObject();
                    writer.WriteString(configKey.Name, value);
                    writer.WriteEndObject();
                }));
                break;

            case OutputFormat.Nul:
                Console.Write(new NulWriter().Field(configKey.Name, value).ToString());
                break;

            default:
                Ui.Out.WriteLine(value);
                break;
        }

        return 0;
    }
}

internal sealed class ConfigSetCommand : Command
{
    private readonly ConfigFile _configFile;

    private readonly Argument<string> _keyArg = new("key")
    {
        Description = "Configuration key to set (e.g. store.backend)."
    };

    private readonly Argument<string> _valueArg = new("value")
    {
        Description = "Value to assign."
    };

    public ConfigSetCommand(ConfigFile configFile)
        : base("set", "Set a configuration value.")
    {
        _configFile = configFile;
        Add(_keyArg);
        Add(_valueArg);
        SetAction(Execute);
    }

    private int Execute(ParseResult result)
    {
        string key = result.GetRequiredValue(_keyArg);
        string value = result.GetRequiredValue(_valueArg);

        if (ConfigKeys.Find(key) is not { } configKey)
        {
            Ui.ReportError(ConfigCommandHelpers.UnknownKey(key));
            return 1;
        }

        AppConfig config = _configFile.Load();
        try
        {
            configKey.Set(config, value);
        }
        catch (FormatException ex)
        {
            Ui.ReportError(ex.Message);
            return 1;
        }

        _configFile.Save(config);

        Ui.Out.MarkupLine(
            $"[green]Set[/] [bold]{Markup.Escape(configKey.Name)}[/] = " +
            $"[bold]{Markup.Escape(configKey.GetEffective(config))}[/].");

        // Warn (but do not fail) if the chosen backend cannot be used here; the
        // factory owns the per-OS rules, so reuse it to validate.
        if (configKey.Name == "store.backend")
        {
            try
            {
                _ = OtpStoreFactory.Create(config.Store.Backend ?? StoreBackend.Auto);
            }
            catch (OtpStoreException ex)
            {
                Ui.Error.MarkupLine($"[yellow]warning:[/] {Markup.Escape(ex.Message)}");
            }
        }

        return 0;
    }
}

internal sealed class ConfigUnsetCommand : Command
{
    private readonly ConfigFile _configFile;

    private readonly Argument<string> _keyArg = new("key")
    {
        Description = "Configuration key to remove (e.g. store.backend)."
    };

    public ConfigUnsetCommand(ConfigFile configFile)
        : base("unset", "Remove a configuration value, reverting it to its default.")
    {
        _configFile = configFile;
        Add(_keyArg);
        SetAction(Execute);
    }

    private int Execute(ParseResult result)
    {
        string key = result.GetRequiredValue(_keyArg);
        if (ConfigKeys.Find(key) is not { } configKey)
        {
            Ui.ReportError(ConfigCommandHelpers.UnknownKey(key));
            return 1;
        }

        AppConfig config = _configFile.Load();
        if (configKey.GetRaw(config) is null)
        {
            Ui.Error.MarkupLine($"[grey]{Markup.Escape(configKey.Name)} is not set.[/]");
            return 0;
        }

        configKey.Unset(config);
        _configFile.Save(config);

        Ui.Out.MarkupLine($"[green]Unset[/] [bold]{Markup.Escape(configKey.Name)}[/].");
        return 0;
    }
}

internal sealed class ConfigListCommand : Command
{
    private readonly ConfigFile _configFile;
    private readonly FormatOptions _format = new();

    public ConfigListCommand(ConfigFile configFile)
        : base("list", "List explicitly-set configuration values.")
    {
        _configFile = configFile;
        Aliases.Add("ls");
        _format.AddTo(this);
        SetAction(Execute);
    }

    private int Execute(ParseResult result)
    {
        AppConfig config = _configFile.Load();
        var entries = ConfigKeys.All
            .Select(key => (key.Name, Value: key.GetRaw(config)))
            .Where(entry => entry.Value is not null)
            .ToList();

        switch (_format.Resolve(result))
        {
            case OutputFormat.Json:
                Console.WriteLine(OtpFormat.Json(writer =>
                {
                    writer.WriteStartObject();
                    foreach ((string name, string? value) in entries)
                    {
                        writer.WriteString(name, value!);
                    }

                    writer.WriteEndObject();
                }));
                break;

            case OutputFormat.Nul:
                var records = new NulWriter();
                foreach ((string name, string? value) in entries)
                {
                    records.Field(name, value!);
                }

                Console.Write(records.ToString());
                break;

            default:
                foreach ((string name, string? value) in entries)
                {
                    Ui.Out.WriteLine($"{name} = {value}");
                }

                break;
        }

        return 0;
    }
}

internal sealed class ConfigEditCommand : Command
{
    private readonly ConfigFile _configFile;

    public ConfigEditCommand(ConfigFile configFile)
        : base("edit", "Open the configuration file in an editor ($VISUAL or $EDITOR).")
    {
        _configFile = configFile;
        SetAction(Execute);
    }

    private int Execute(ParseResult result)
    {
        // Ensure the file exists so the editor opens something concrete.
        AppConfig config = _configFile.Load();
        if (!File.Exists(_configFile.FilePath))
        {
            _configFile.Save(config);
        }

        string editor = ResolveEditor();
        string file = _configFile.FilePath;

        ProcessStartInfo startInfo = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("cmd.exe") { ArgumentList = { "/c", $"{editor} \"{file}\"" } }
            : new ProcessStartInfo("/bin/sh") { ArgumentList = { "-c", $"{editor} \"{file}\"" } };
        startInfo.UseShellExecute = false;

        try
        {
            using Process? process = Process.Start(startInfo);
            process?.WaitForExit();

            if (process is { ExitCode: not 0 })
            {
                Ui.ReportError($"editor exited with code {process.ExitCode}; configuration left unchanged.");
                return process.ExitCode;
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Ui.ReportError($"failed to launch editor '{editor}': {ex.Message}");
            return 1;
        }

        // Surface (but do not repair) a config the user may have broken.
        try
        {
            _configFile.Load();
        }
        catch (JsonException ex)
        {
            Ui.ReportError($"the configuration file is no longer valid JSON: {ex.Message}");
            return 1;
        }

        return 0;
    }

    private static string ResolveEditor()
    {
        string? visual = Environment.GetEnvironmentVariable("VISUAL");
        if (!string.IsNullOrWhiteSpace(visual))
        {
            return visual;
        }

        string? editor = Environment.GetEnvironmentVariable("EDITOR");
        if (!string.IsNullOrWhiteSpace(editor))
        {
            return editor;
        }

        return OperatingSystem.IsWindows() ? "notepad" : "vi";
    }
}

internal static class ConfigCommandHelpers
{
    public static string UnknownKey(string key) =>
        $"unknown config key '{key}'. Known keys: {string.Join(", ", ConfigKeys.All.Select(k => k.Name))}.";
}
