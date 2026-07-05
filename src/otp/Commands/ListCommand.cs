using System.CommandLine;
using Mjcheetham.Otp.Storage;
using Spectre.Console;

namespace Mjcheetham.Otp.Commands;

public class ListCommand : Command
{
    private readonly Lazy<IOtpStore> _store;
    private readonly FormatOptions _format = new();

    private readonly Option<OtpKind?> _typeOpt = new("--type", "-t")
    {
        Description = "Show only one-time passwords of the specified type (totp or hotp).",
        HelpName = "totp|hotp",
        CustomParser = OtpTypeParser.ParseNullable
    };

    public ListCommand(Lazy<IOtpStore> store) : base("list", "List stored one-time passwords.")
    {
        _store = store;

        Aliases.Add("ls");

        Add(_typeOpt);
        _format.AddTo(this);
        SetAction((result, cancellationToken) => StoreActions.RunAsync(() => ExecuteAsync(result, cancellationToken)));
    }

    private async Task<int> ExecuteAsync(ParseResult result, CancellationToken cancellationToken)
    {
        OtpKind? type = result.GetValue(_typeOpt);
        OutputFormat format = _format.Resolve(result);

        var names = new List<string>();
        await foreach (var otp in _store.Value.ListAsync(cancellationToken))
        {
            if (type is not null && otp.Kind != type)
            {
                continue;
            }

            names.Add(otp.Name);
        }

        switch (format)
        {
            case OutputFormat.Json:
                Console.WriteLine(OtpFormat.Json(writer =>
                {
                    writer.WriteStartArray();
                    foreach (string name in names)
                    {
                        writer.WriteStringValue(name);
                    }

                    writer.WriteEndArray();
                }));
                break;

            case OutputFormat.Nul:
                var records = new NulWriter();
                foreach (string name in names)
                {
                    records.Item(name);
                }

                Console.Write(records.ToString());
                break;

            default:
                foreach (string name in names)
                {
                    Ui.Out.WriteLine(name);
                }

                break;
        }

        return 0;
    }
}
