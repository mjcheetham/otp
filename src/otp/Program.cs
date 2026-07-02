using System.CommandLine;
using System.CommandLine.Parsing;
using Mjcheetham.Otp;
using Mjcheetham.Otp.Commands;

var store = new InMemoryOtpStore();

var rootCommand = new RootCommand("Create and manage one-time passwords (OTPs).");
rootCommand.Add(new AddCommand());
rootCommand.Add(new ListCommand(store));
rootCommand.Add(new GetCommand());
rootCommand.Add(new RemoveCommand());

return await CommandLineParser.Parse(rootCommand, args).InvokeAsync();
