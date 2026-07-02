using System.CommandLine;

namespace Mjcheetham.Otp.Commands;

public class ListCommand : Command
{
    private readonly IOtpStore _store;

    private readonly Option<OtpKind?> _typeOpt = new("--type", "-t")
    {
        Description = "Show only one-time passwords of the specified type (totp or hotp).",
        HelpName = "totp|hotp",
        CustomParser = OtpTypeParser.ParseNullable
    };

    public ListCommand(IOtpStore store) : base("list", "List stored one-time passwords.")
    {
        _store = store;

        Aliases.Add("ls");

        Add(_typeOpt);
        SetAction(ExecuteAsync);
    }

    private async Task<int> ExecuteAsync(ParseResult r, CancellationToken ct)
    {
        OtpKind? type = r.GetValue(_typeOpt);

        await foreach (var otp in _store.ListAsync(ct))
        {
            if (type is not null && otp.Kind != type)
            {
                continue;
            }

            Console.WriteLine(otp.Name);
        }

        return 0;
    }
}
