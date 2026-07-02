using System.CommandLine;
using System.Globalization;
using Spectre.Console;

namespace Mjcheetham.Otp.Commands;

public class GetCommand : Command
{
    private readonly IOtpStore _store;
    private readonly FormatOptions _format = new();

    private readonly Argument<string> _nameArg = new("name")
    {
        Description = "Name of the one-time password."
    };

    private readonly Option<long?> _counterOpt = new("--counter", "-c")
    {
        Description = "Counter value to use for this code (counter-based OTPs only)."
    };

    public GetCommand(IOtpStore store) : base("get", "Generate the current code for a stored one-time password.")
    {
        _store = store;

        Aliases.Add("code");

        Add(_nameArg);
        Add(_counterOpt);
        _format.AddTo(this);

        SetAction(ExecuteAsync);
    }

    private async Task<int> ExecuteAsync(ParseResult result, CancellationToken cancellationToken)
    {
        string name = result.GetRequiredValue(_nameArg);
        long? counter = result.GetValue(_counterOpt);
        OutputFormat format = _format.Resolve(result);

        IOneTimePassword? otp = await _store.GetAsync(name, cancellationToken);
        if (otp is null)
        {
            Ui.ReportError($"no one-time password named '{name}' was found.");
            return 1;
        }

        if (counter is not null && otp is not HmacOtp)
        {
            Ui.ReportError("--counter applies to counter-based (hotp) one-time passwords only.");
            return 1;
        }

        string code;
        int? validForSeconds = null;
        DateTimeOffset? expiresAt = null;

        switch (otp)
        {
            case TimeBasedOtp timeBased:
                DateTimeOffset now = DateTimeOffset.UtcNow;
                code = timeBased.GetCode(now);
                long nextBoundary = (now.ToUnixTimeSeconds() / timeBased.Period + 1) * timeBased.Period;
                expiresAt = DateTimeOffset.FromUnixTimeSeconds(nextBoundary);
                validForSeconds = (int)(nextBoundary - now.ToUnixTimeSeconds());
                break;

            case HmacOtp counterBased:
                code = counter is not null ? counterBased.GetCode(counter.Value) : counterBased.GetCode();
                break;

            default:
                code = otp.GetCode();
                break;
        }

        switch (format)
        {
            case OutputFormat.Json:
                Console.WriteLine(OtpFormat.Json(writer =>
                {
                    writer.WriteStartObject();
                    writer.WriteString("code", code);
                    writer.WriteString("type", OtpFormat.TypeMachine(otp.Kind));
                    if (validForSeconds is not null)
                    {
                        writer.WriteNumber("valid_for_seconds", validForSeconds.Value);
                        writer.WriteString("expires_at", OtpFormat.IsoUtc(expiresAt!.Value));
                    }
                    else if (otp is HmacOtp counterBased)
                    {
                        writer.WriteNumber("counter", counter ?? counterBased.Counter);
                    }

                    writer.WriteEndObject();
                }));
                break;

            case OutputFormat.Nul:
                var records = new NulWriter()
                    .Field("code", code)
                    .Field("type", OtpFormat.TypeMachine(otp.Kind));
                if (validForSeconds is not null)
                {
                    records.Field("valid_for_seconds", validForSeconds.Value.ToString(CultureInfo.InvariantCulture));
                    records.Field("expires_at", OtpFormat.IsoUtc(expiresAt!.Value));
                }
                else if (otp is HmacOtp counterBased)
                {
                    records.Field("counter", (counter ?? counterBased.Counter).ToString(CultureInfo.InvariantCulture));
                }

                Console.Write(records.ToString());
                break;

            default:
                Ui.Out.MarkupLine($"[bold]{Markup.Escape(code)}[/]");
                if (validForSeconds is not null)
                {
                    string style = validForSeconds.Value <= 5 ? "yellow" : "grey";
                    Ui.Error.MarkupLine($"[{style}]Valid for {validForSeconds.Value}s[/]");
                }

                break;
        }

        return 0;
    }
}
