using Spectre.Console;

namespace Mjcheetham.Otp.Commands;

/// <summary>
/// Human-facing console endpoints rendered through Spectre.Console. Standard
/// output uses the ambient console; errors, prompts, and notes go to standard
/// error. Machine-readable output (JSON, NUL) must bypass these and be written
/// directly to the standard streams so it is never styled or wrapped.
/// </summary>
internal static class Ui
{
    public static IAnsiConsole Out => AnsiConsole.Console;

    public static IAnsiConsole Error { get; } = AnsiConsole.Create(new AnsiConsoleSettings
    {
        Out = new AnsiConsoleOutput(Console.Error)
    });

    public static void ReportError(string message) =>
        Error.MarkupLine($"[red]error:[/] {Markup.Escape(message)}");
}
