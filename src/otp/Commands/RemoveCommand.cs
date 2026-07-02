using System.CommandLine;
using Spectre.Console;

namespace Mjcheetham.Otp.Commands;

public class RemoveCommand : Command
{
    private readonly IOtpStore _store;

    private readonly Argument<string> _nameArg = new("name")
    {
        Description = "Name of the one-time password to remove."
    };

    private readonly Option<bool> _yesOpt = new("--yes", "-y")
    {
        Description = "Remove without prompting for confirmation."
    };

    public RemoveCommand(IOtpStore store) : base("remove", "Remove a stored one-time password.")
    {
        _store = store;

        Aliases.Add("rm");

        Add(_nameArg);
        Add(_yesOpt);

        SetAction(ExecuteAsync);
    }

    private async Task<int> ExecuteAsync(ParseResult result, CancellationToken cancellationToken)
    {
        string name = result.GetValue(_nameArg)!;
        bool skipConfirmation = result.GetValue(_yesOpt);

        IOneTimePassword? otp = await _store.GetAsync(name, cancellationToken);
        if (otp is null)
        {
            Ui.Error.WriteLine($"error: no one-time password named '{name}' was found.");
            return 1;
        }

        if (!skipConfirmation && !Confirm(otp))
        {
            Ui.Error.WriteLine("Aborted.");
            return 0;
        }

        if (!await _store.RemoveAsync(otp.Name, cancellationToken))
        {
            Ui.Error.WriteLine($"error: no one-time password named '{name}' was found.");
            return 1;
        }

        Ui.Out.WriteLine($"Removed '{otp.Name}'.");
        return 0;
    }

    private static bool Confirm(IOneTimePassword otp)
    {
        string label = otp.Issuer is { Length: > 0 } issuer
            ? $"'{otp.Name}' ({issuer})"
            : $"'{otp.Name}'";

        Ui.Error.Write($"Remove one-time password {label}? [y/N] ");

        string? answer = Console.ReadLine()?.Trim();
        return string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase);
    }
}
