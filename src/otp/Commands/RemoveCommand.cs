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
            Ui.ReportError($"no one-time password named '{name}' was found.");
            return 1;
        }

        if (!skipConfirmation)
        {
            if (Console.IsInputRedirected || !Ui.Error.Profile.Capabilities.Interactive)
            {
                Ui.ReportError($"refusing to remove '{name}' without confirmation; re-run with --yes.");
                return 1;
            }

            if (!Confirm(otp))
            {
                return 1;
            }
        }

        if (!await _store.RemoveAsync(otp.Name, cancellationToken))
        {
            Ui.ReportError($"no one-time password named '{name}' was found.");
            return 1;
        }

        Ui.Out.MarkupLine($"[green]Removed[/] [bold]'{Markup.Escape(otp.Name)}'[/].");
        return 0;
    }

    private static bool Confirm(IOneTimePassword otp)
    {
        string label = otp.Issuer is { Length: > 0 } issuer
            ? $"'{otp.Name}' ({issuer})"
            : $"'{otp.Name}'";

        return Ui.Error.Confirm($"Remove one-time password {Markup.Escape(label)}?", defaultValue: false);
    }
}
