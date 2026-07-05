using System.CommandLine;
using System.CommandLine.Parsing;
using Mjcheetham.Otp;
using Mjcheetham.Otp.Commands;
using Mjcheetham.Otp.Config;
using Mjcheetham.Otp.Storage;

var configFile = new ConfigFile(ConfigFile.GetDefaultPath());
var config = configFile.Load();

// The store is resolved lazily so `config` (and --help) work even when the
// configured backend is invalid or unavailable on this machine.
var store = new Lazy<IOtpStore>(() => StoreResolver.CreateStore(config));

var rootCommand = new RootCommand("Create and manage one-time passwords (OTPs).");
rootCommand.Add(new AddCommand(store));
rootCommand.Add(new ListCommand(store));
rootCommand.Add(new GetCommand(store));
rootCommand.Add(new ShowCommand(store));
rootCommand.Add(new RemoveCommand(store));
rootCommand.Add(new ConfigCommand(configFile));

// Disable colourful output if NO_COLOR is set
if (IsEnvar("NO_COLOR", false))
    Ui.DisableColor();

// Disable ANSI escape sequences if NO_ANSI is set
if (IsEnvar("NO_ANSI", false))
    Ui.DisableAnsi();

return await CommandLineParser.Parse(rootCommand, args).InvokeAsync();

static bool IsEnvar(string name, bool defaultValue)
{
    string? value = Environment.GetEnvironmentVariable(name);

    if (StringComparer.OrdinalIgnoreCase.Equals(value, "1") ||
        StringComparer.OrdinalIgnoreCase.Equals(value, "true") ||
        StringComparer.OrdinalIgnoreCase.Equals(value, "yes") ||
        StringComparer.OrdinalIgnoreCase.Equals(value, "on"))
    {
        return true;
    }

    if (StringComparer.OrdinalIgnoreCase.Equals(value, "0") ||
        StringComparer.OrdinalIgnoreCase.Equals(value, "false") ||
        StringComparer.OrdinalIgnoreCase.Equals(value, "no") ||
        StringComparer.OrdinalIgnoreCase.Equals(value, "off"))
    {
        return false;
    }

    return defaultValue;
}
