using System.CommandLine;

namespace Mjcheetham.Otp.Commands;

public class GetCommand : Command
{
    private readonly IOtpStore _store;

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

        SetAction(ExecuteAsync);
    }

    private async Task<int> ExecuteAsync(ParseResult result, CancellationToken cancellationToken)
    {
        string name = result.GetRequiredValue(_nameArg);
        long? counter = result.GetValue(_counterOpt);

        IOneTimePassword? otp = await _store.GetAsync(name, cancellationToken);
        if (otp is null)
        {
            Console.Error.WriteLine($"error: no one-time password named '{name}' was found.");
            return 1;
        }

        string code;
        if (counter is not null)
        {
            if (otp is HmacOtp hotp)
            {
                code = hotp.GetCode(counter.Value);
            }
            else
            {
                Console.Error.WriteLine("error: --counter applies to counter-based (hotp) one-time passwords only.");
                return 1;
            }
        }
        else
        {
            code = otp.GetCode();
        }

        Console.WriteLine(code);
        return 0;
    }
}
