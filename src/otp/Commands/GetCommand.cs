using System.CommandLine;

namespace Mjcheetham.Otp.Commands;

public class GetCommand : Command
{
    private readonly Argument<string> _nameArg = new("name")
    {
        Description = "Name of the one-time password."
    };

    private readonly Option<long?> _counterOpt = new("--counter", "-c")
    {
        Description = "Counter value to use for this code (counter-based OTPs only)."
    };

    public GetCommand() : base("get", "Generate the current code for a stored one-time password.")
    {
        Aliases.Add("code");

        Add(_nameArg);
        Add(_counterOpt);

        SetAction(ExecuteAsync);
    }

    private Task<int> ExecuteAsync(ParseResult result, CancellationToken cancellationToken)
    {
        Console.Error.WriteLine("otp get: not yet implemented.");
        return Task.FromResult(1);
    }
}
