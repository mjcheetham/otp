using System.CommandLine;
using System.CommandLine.Parsing;
using Spectre.Console;

namespace Mjcheetham.Otp.Commands;

public class AddCommand : Command
{
    private readonly IOtpStore _store;

    private readonly Argument<string> _nameArg = new("name")
    {
        Description = "Name/label to store the one-time password under. " +
                      "Required unless --uri is specified.",
        Arity = ArgumentArity.ZeroOrOne
    };

    private readonly Option<string> _uriOpt = new("--uri")
    {
        Description = "Import from an otpauth:// URI. Cannot be combined with a " +
                      "name argument or the individual value options below."
    };

    private readonly Option<bool> _interactiveOpt = new("--interactive", "-i")
    {
        Description = "Prompt for each field interactively. Cannot be combined with a " +
                      "name argument, --uri, or the individual value options below."
    };

    private readonly Option<string> _secretOpt = new("--secret", "-s")
    {
        Description = "Shared secret, Base32-encoded. Required unless --uri is specified."
    };

    private readonly Option<OtpKind> _typeOpt = new("--type", "-t")
    {
        Description = "Type of one-time password: 'totp' (time-based) or " +
                      "'hotp' (counter-based). Default: totp.",
        HelpName = "totp|hotp",
        CustomParser = OtpTypeParser.Parse
    };

    private readonly Option<string> _issuerOpt = new("--issuer")
    {
        Description = "Issuer or provider name."
    };

    private readonly Option<int> _digitsOpt = new("--digits", "-d")
    {
        Description = "Number of digits in the generated code. Default: 6.",
        DefaultValueFactory = _ => 6
    };

    private readonly Option<int> _periodOpt = new("--period", "-p")
    {
        Description = "Time step in seconds (time-based OTPs only). Default: 30.",
        DefaultValueFactory = _ => 30
    };

    private readonly Option<long> _counterOpt = new("--counter", "-c")
    {
        Description = "Initial counter value (counter-based OTPs only). Default: 0.",
        DefaultValueFactory = _ => 0L
    };

    private readonly Option<OtpAlgorithm> _algorithmOpt = new("--algorithm", "-a")
    {
        Description = "HMAC hash algorithm: sha1, sha256, or sha512. Default: sha1.",
        HelpName = "sha1|sha256|sha512"
    };

    public AddCommand(IOtpStore store) : base("add", "Add a new one-time password.")
    {
        _store = store;

        Add(_nameArg);
        Add(_uriOpt);
        Add(_interactiveOpt);
        Add(_secretOpt);
        Add(_typeOpt);
        Add(_issuerOpt);
        Add(_digitsOpt);
        Add(_periodOpt);
        Add(_counterOpt);
        Add(_algorithmOpt);

        Validators.Add(Validate);
        SetAction(ExecuteAsync);
    }

    private void Validate(CommandResult result)
    {
        string? name = result.GetValue(_nameArg);
        bool hasUri = !string.IsNullOrWhiteSpace(result.GetValue(_uriOpt));

        if (result.GetValue(_interactiveOpt))
        {
            if (!string.IsNullOrWhiteSpace(name) ||
                hasUri ||
                result.GetResult(_secretOpt) is not null ||
                result.GetResult(_issuerOpt) is not null ||
                result.GetResult(_typeOpt) is not null ||
                result.GetResult(_digitsOpt) is not null ||
                result.GetResult(_periodOpt) is not null ||
                result.GetResult(_counterOpt) is not null ||
                result.GetResult(_algorithmOpt) is not null)
            {
                result.AddError("--interactive cannot be combined with a name argument, --uri, or the individual value options.");
            }

            if (!IsInteractiveTerminal())
            {
                result.AddError("--interactive requires an interactive terminal.");
            }

            return;
        }

        if (hasUri)
        {
            if (!string.IsNullOrWhiteSpace(name) ||
                result.GetResult(_secretOpt) is not null ||
                result.GetResult(_issuerOpt) is not null ||
                result.GetResult(_typeOpt) is not null ||
                result.GetResult(_digitsOpt) is not null ||
                result.GetResult(_periodOpt) is not null ||
                result.GetResult(_counterOpt) is not null ||
                result.GetResult(_algorithmOpt) is not null)
            {
                result.AddError("--uri cannot be combined with a name argument or the individual value options.");
            }

            return;
        }

        // With no explicit inputs, fall back to interactive prompts when the
        // terminal can support them; the action re-checks this same condition.
        if (IsBare(result) && IsInteractiveTerminal())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            result.AddError("A name argument is required unless --uri is specified.");
        }

        string? secret = result.GetValue(_secretOpt);
        if (string.IsNullOrWhiteSpace(secret))
        {
            result.AddError("--secret is required unless --uri is specified.");
        }
        else if (!Base32.TryDecode(secret, out byte[]? secretBytes, out string? secretError))
        {
            result.AddError(secretError);
        }
        else if (secretBytes.Length == 0)
        {
            result.AddError("The secret does not contain any data.");
        }

        if (result.GetResult(_digitsOpt) is not null &&
            !OtpGenerator.TryValidateDigits(result.GetValue(_digitsOpt), out string? digitsError))
        {
            result.AddError(digitsError);
        }

        if (result.GetValue(_typeOpt) == OtpKind.Hmac)
        {
            if (result.GetResult(_periodOpt) is not null)
            {
                result.AddError("--period applies to time-based (totp) one-time passwords only.");
            }

            if (result.GetResult(_counterOpt) is not null &&
                !HmacOtp.TryValidateCounter(result.GetValue(_counterOpt), out string? counterError))
            {
                result.AddError(counterError);
            }
        }
        else
        {
            if (result.GetResult(_counterOpt) is not null)
            {
                result.AddError("--counter applies to counter-based (hotp) one-time passwords only.");
            }

            if (result.GetResult(_periodOpt) is not null &&
                !TimeBasedOtp.TryValidatePeriod(result.GetValue(_periodOpt), out string? periodError))
            {
                result.AddError(periodError);
            }
        }
    }

    private async Task<int> ExecuteAsync(ParseResult result, CancellationToken cancellationToken)
    {
        IOneTimePassword otp;

        if (ShouldRunInteractive(result.CommandResult))
        {
            try
            {
                otp = await BuildInteractiveAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // A SelectionPrompt hides the cursor and only restores it on a
                // normal return, so put it back before bailing out.
                Ui.Error.Cursor.Show();
                Ui.Error.WriteLine();
                Ui.Error.MarkupLine("[grey]Cancelled.[/]");
                return 130;
            }
        }
        else
        {
            try
            {
                string? uri = result.GetValue(_uriOpt);
                otp = !string.IsNullOrWhiteSpace(uri)
                    ? OtpAuthUri.Parse(uri)
                    : BuildFromOptions(result);
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException)
            {
                Ui.ReportError(ex.Message);
                return 1;
            }
        }

        try
        {
            await _store.AddAsync(otp, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            Ui.ReportError(ex.Message);
            return 1;
        }

        Ui.Out.MarkupLine($"[green]Added[/] [bold]'{Markup.Escape(otp.Name)}'[/].");
        return 0;
    }

    private IOneTimePassword BuildFromOptions(ParseResult result)
    {
        string name = result.GetValue(_nameArg)!;
        byte[] secret = Base32.Decode(result.GetValue(_secretOpt)!);
        OtpKind kind = result.GetValue(_typeOpt);
        string? issuer = result.GetValue(_issuerOpt);
        int digits = result.GetValue(_digitsOpt);
        OtpAlgorithm algorithm = result.GetValue(_algorithmOpt);

        return kind == OtpKind.Hmac
            ? new HmacOtp(name, secret, result.GetValue(_counterOpt), digits, algorithm, issuer)
            : new TimeBasedOtp(name, secret, result.GetValue(_periodOpt), digits, algorithm, issuer);
    }

    private bool ShouldRunInteractive(CommandResult result) =>
        result.GetValue(_interactiveOpt) || (IsBare(result) && IsInteractiveTerminal());

    private bool IsBare(CommandResult result) =>
        result.GetResult(_nameArg) is not { Implicit: false } &&
        result.GetResult(_uriOpt) is not { Implicit: false } &&
        result.GetResult(_secretOpt) is not { Implicit: false } &&
        result.GetResult(_issuerOpt) is not { Implicit: false } &&
        result.GetResult(_typeOpt) is not { Implicit: false } &&
        result.GetResult(_digitsOpt) is not { Implicit: false } &&
        result.GetResult(_periodOpt) is not { Implicit: false } &&
        result.GetResult(_counterOpt) is not { Implicit: false } &&
        result.GetResult(_algorithmOpt) is not { Implicit: false };

    private static bool IsInteractiveTerminal() =>
        !Console.IsInputRedirected && Ui.Error.Profile.Capabilities.Interactive;

    private async Task<IOneTimePassword> BuildInteractiveAsync(CancellationToken cancellationToken)
    {
        IAnsiConsole io = Ui.Error;

        var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await foreach (IOneTimePassword existing in _store.ListAsync(cancellationToken))
        {
            existingNames.Add(existing.Name);
        }

        io.MarkupLine("[grey]Adding a new one-time password. Press Ctrl+C to cancel.[/]");

        static ValidationResult Fail(string message) =>
            ValidationResult.Error($"[red]{Markup.Escape(message)}[/]");

        string name = (await io.PromptAsync(
            new TextPrompt<string>("Name:")
                .Validate(value =>
                {
                    string trimmed = value.Trim();
                    if (trimmed.Length == 0)
                    {
                        return Fail("A name is required.");
                    }

                    return existingNames.Contains(trimmed)
                        ? Fail($"A one-time password named '{trimmed}' already exists.")
                        : ValidationResult.Success();
                }), cancellationToken)).Trim();

        string issuerInput = await io.PromptAsync(
            new TextPrompt<string>("Issuer [grey](optional)[/]:")
                .AllowEmpty(), cancellationToken);
        string? issuer = string.IsNullOrWhiteSpace(issuerInput) ? null : issuerInput.Trim();

        OtpKind kind = await io.PromptChoiceAsync("Type:", OtpFormat.TypeHuman,
            cancellationToken, OtpKind.TimeBased, OtpKind.Hmac);

        string secretInput = await io.PromptAsync(
            new TextPrompt<string>("Secret [grey](Base32)[/]:")
                .Validate(value =>
                {
                    if (!Base32.TryDecode(value, out byte[]? bytes, out string? error))
                    {
                        return Fail(error);
                    }

                    return bytes.Length == 0
                        ? Fail("The secret does not contain any data.")
                        : ValidationResult.Success();
                }), cancellationToken);
        byte[] secret = Base32.Decode(secretInput);

        int digits = await io.PromptAsync(
            new TextPrompt<int>("Digits")
                .DefaultValue(6)
                .Validate(value => OtpGenerator.TryValidateDigits(value, out string? error)
                    ? ValidationResult.Success()
                    : Fail(error)), cancellationToken);

        OtpAlgorithm algorithm = await io.PromptChoiceAsync("Algorithm:", OtpFormat.AlgorithmHuman,
            cancellationToken, OtpAlgorithm.Sha1, OtpAlgorithm.Sha256, OtpAlgorithm.Sha512);

        if (kind == OtpKind.Hmac)
        {
            long counter = await io.PromptAsync(
                new TextPrompt<long>("Initial counter")
                    .DefaultValue(0L)
                    .Validate(value => HmacOtp.TryValidateCounter(value, out string? error)
                        ? ValidationResult.Success()
                        : Fail(error)), cancellationToken);

            return new HmacOtp(name, secret, counter, digits, algorithm, issuer);
        }

        int period = await io.PromptAsync(
            new TextPrompt<int>("Period in [grey]seconds[/]")
                .DefaultValue(30)
                .Validate(value => TimeBasedOtp.TryValidatePeriod(value, out string? error)
                    ? ValidationResult.Success()
                    : Fail(error)), cancellationToken);

        return new TimeBasedOtp(name, secret, period, digits, algorithm, issuer);
    }
}
