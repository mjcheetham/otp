using System.CommandLine;
using System.CommandLine.Parsing;
using Mjcheetham.Otp;
using Mjcheetham.Otp.Commands;

var store = new FileOtpStore(FileOtpStore.GetDefaultPath());

var rootCommand = new RootCommand("Create and manage one-time passwords (OTPs).");
rootCommand.Add(new AddCommand(store));
rootCommand.Add(new ListCommand(store));
rootCommand.Add(new GetCommand(store));
rootCommand.Add(new RemoveCommand(store));

return await CommandLineParser.Parse(rootCommand, args).InvokeAsync();
