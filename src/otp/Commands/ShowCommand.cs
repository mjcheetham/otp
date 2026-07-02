using System.CommandLine;
using System.Text;

namespace Mjcheetham.Otp.Commands;

public class ShowCommand : Command
{
    private const string MaskedSecret = "••••••••";

    private readonly IOtpStore _store;

    private readonly Argument<string> _nameArg = new("name")
    {
        Description = "Name of the one-time password."
    };

    private readonly Option<bool> _showSecretOpt = new("--show-secret")
    {
        Description = "Reveal the shared secret instead of masking it."
    };

    public ShowCommand(IOtpStore store) : base("show", "Show the details of a stored one-time password.")
    {
        _store = store;

        Add(_nameArg);
        Add(_showSecretOpt);

        SetAction(ExecuteAsync);
    }

    private async Task<int> ExecuteAsync(ParseResult result, CancellationToken cancellationToken)
    {
        string name = result.GetRequiredValue(_nameArg);
        bool showSecret = result.GetValue(_showSecretOpt);

        IOneTimePassword? otp = await _store.GetAsync(name, cancellationToken);
        if (otp is null)
        {
            Console.Error.WriteLine($"error: no one-time password named '{name}' was found.");
            return 1;
        }

        var output = new StringBuilder();
        void Line(string label, string value) => output.AppendLine($"{label,-10} {value}");

        Line("Name:", otp.Name);
        if (!string.IsNullOrEmpty(otp.Issuer))
        {
            Line("Issuer:", otp.Issuer);
        }

        Line("Type:", otp.Kind == OtpKind.Hmac ? "hotp" : "totp");
        Line("Algorithm:", otp.Algorithm.ToString().ToLowerInvariant());
        Line("Digits:", otp.Digits.ToString());

        switch (otp)
        {
            case TimeBasedOtp totp:
                Line("Period:", $"{totp.Period}s");
                break;
            case HmacOtp hotp:
                Line("Counter:", hotp.Counter.ToString());
                break;
        }

        Line("Secret:", showSecret ? otp.GetSecret() : MaskedSecret);

        Console.Write(output.ToString());
        return 0;
    }
}
