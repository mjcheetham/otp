using System.CommandLine;
using System.CommandLine.Parsing;

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

    private readonly Option<string> _uriOpt = new("--uri", "-u")
    {
        Description = "Import from an otpauth:// URI. Cannot be combined with a " +
                      "name argument or the individual value options below."
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

    private readonly Option<string> _issuerOpt = new("--issuer", "-i")
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

        if (string.IsNullOrWhiteSpace(name))
        {
            result.AddError("A name argument is required unless --uri is specified.");
        }

        if (string.IsNullOrWhiteSpace(result.GetValue(_secretOpt)))
        {
            result.AddError("--secret is required unless --uri is specified.");
        }

        bool isCounterBased = result.GetValue(_typeOpt) == OtpKind.Hmac;

        if (isCounterBased && result.GetResult(_periodOpt) is not null)
        {
            result.AddError("--period applies to time-based (totp) one-time passwords only.");
        }

        if (!isCounterBased && result.GetResult(_counterOpt) is not null)
        {
            result.AddError("--counter applies to counter-based (hotp) one-time passwords only.");
        }
    }

    private async Task<int> ExecuteAsync(ParseResult result, CancellationToken cancellationToken)
    {
        IOneTimePassword otp;
        try
        {
            string? uri = result.GetValue(_uriOpt);
            otp = !string.IsNullOrWhiteSpace(uri)
                ? OtpAuthUri.Parse(uri)
                : BuildFromOptions(result);
        }
        catch (FormatException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }

        try
        {
            await _store.AddAsync(otp, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }

        Console.WriteLine($"Added '{otp.Name}'.");
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
}
