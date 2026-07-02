using System.CommandLine;
using System.Globalization;
using System.Text;
using Spectre.Console;

namespace Mjcheetham.Otp.Commands;

public class ShowCommand : Command
{
    private const string MaskedSecret = "••••••••";

    private readonly IOtpStore _store;
    private readonly FormatOptions _format = new();

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
        _format.AddTo(this);

        SetAction(ExecuteAsync);
    }

    private async Task<int> ExecuteAsync(ParseResult result, CancellationToken cancellationToken)
    {
        string name = result.GetRequiredValue(_nameArg);
        bool showSecret = result.GetValue(_showSecretOpt);
        OutputFormat format = _format.Resolve(result);

        IOneTimePassword? otp = await _store.GetAsync(name, cancellationToken);
        if (otp is null)
        {
            Ui.Error.WriteLine($"error: no one-time password named '{name}' was found.");
            return 1;
        }

        string? issuer = string.IsNullOrEmpty(otp.Issuer) ? null : otp.Issuer;
        int? period = otp is TimeBasedOtp timeBased ? timeBased.Period : null;
        long? counter = otp is HmacOtp counterBased ? counterBased.Counter : null;
        string? secret = showSecret ? otp.GetSecret() : null;

        switch (format)
        {
            case OutputFormat.Json:
                Console.WriteLine(OtpFormat.Json(writer =>
                {
                    writer.WriteStartObject();
                    writer.WriteString("name", otp.Name);
                    if (issuer is not null)
                    {
                        writer.WriteString("issuer", issuer);
                    }
                    else
                    {
                        writer.WriteNull("issuer");
                    }

                    writer.WriteString("type", OtpFormat.TypeMachine(otp.Kind));
                    writer.WriteString("algorithm", OtpFormat.AlgorithmMachine(otp.Algorithm));
                    writer.WriteNumber("digits", otp.Digits);
                    if (period is not null)
                    {
                        writer.WriteNumber("period", period.Value);
                    }

                    if (counter is not null)
                    {
                        writer.WriteNumber("counter", counter.Value);
                    }

                    if (secret is not null)
                    {
                        writer.WriteString("secret", secret);
                    }
                    else
                    {
                        writer.WriteNull("secret");
                    }

                    writer.WriteEndObject();
                }));
                break;

            case OutputFormat.Nul:
                var records = new NulWriter().Field("name", otp.Name);
                if (issuer is not null)
                {
                    records.Field("issuer", issuer);
                }

                records.Field("type", OtpFormat.TypeMachine(otp.Kind))
                       .Field("algorithm", OtpFormat.AlgorithmMachine(otp.Algorithm))
                       .Field("digits", otp.Digits.ToString(CultureInfo.InvariantCulture));
                if (period is not null)
                {
                    records.Field("period", period.Value.ToString(CultureInfo.InvariantCulture));
                }

                if (counter is not null)
                {
                    records.Field("counter", counter.Value.ToString(CultureInfo.InvariantCulture));
                }

                if (secret is not null)
                {
                    records.Field("secret", secret);
                }

                Console.Write(records.ToString());
                break;

            default:
                var output = new StringBuilder();
                void Line(string label, string value) => output.AppendLine($"{label,-11}{value}");

                Line("Name:", otp.Name);
                if (issuer is not null)
                {
                    Line("Issuer:", issuer);
                }

                Line("Type:", OtpFormat.TypeHuman(otp.Kind));
                Line("Algorithm:", OtpFormat.AlgorithmHuman(otp.Algorithm));
                Line("Digits:", otp.Digits.ToString(CultureInfo.InvariantCulture));
                if (period is not null)
                {
                    Line("Period:", $"{period.Value} seconds");
                }

                if (counter is not null)
                {
                    Line("Counter:", counter.Value.ToString(CultureInfo.InvariantCulture));
                }

                Line("Secret:", showSecret ? otp.GetSecret() : MaskedSecret);
                Ui.Out.Write(output.ToString());
                break;
        }

        return 0;
    }
}
