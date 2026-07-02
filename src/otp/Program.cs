using System.CommandLine;
using System.CommandLine.Parsing;

var rootCommand = new RootCommand("Create and manage one-time passwords (OTPs).");

return await CommandLineParser.Parse(rootCommand, args).InvokeAsync();
