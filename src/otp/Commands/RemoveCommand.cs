using System.CommandLine;

namespace Mjcheetham.Otp.Commands;

public class RemoveCommand : Command
{
    private readonly Argument<string> _nameArg = new("name")
    {
        Description = "Name of the one-time password to remove."
    };

    private readonly Option<bool> _yesOpt = new("--yes", "-y")
    {
        Description = "Remove without prompting for confirmation."
    };

    public RemoveCommand() : base("remove", "Remove a stored one-time password.")
    {
        Aliases.Add("rm");

        Add(_nameArg);
        Add(_yesOpt);

        SetAction(ExecuteAsync);
    }

    private Task<int> ExecuteAsync(ParseResult result, CancellationToken cancellationToken)
    {
        Console.Error.WriteLine("otp remove: not yet implemented.");
        return Task.FromResult(1);
    }
}
