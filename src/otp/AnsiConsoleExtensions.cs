using Spectre.Console;

namespace Mjcheetham.Otp;

internal static class AnsiConsoleExtensions
{
    /// <summary>
    /// Prompts the user to pick one of <paramref name="choices"/> with a
    /// <see cref="SelectionPrompt{T}"/>, then echoes the chosen value so it stays
    /// on screen. A <see cref="SelectionPrompt{T}"/> always erases its interactive
    /// list once a choice is made (it has no <c>ClearOnFinish</c> toggle, unlike
    /// <see cref="TextPrompt{T}"/>), so the echo mirrors how a text prompt leaves
    /// its answer behind.
    /// </summary>
    public static async Task<T> PromptChoiceAsync<T>(
        this IAnsiConsole console,
        string title,
        Func<T, string> converter,
        CancellationToken cancellationToken,
        params T[] choices)
        where T : notnull
    {
        T value = await console.PromptAsync(
            new SelectionPrompt<T>()
                .Title(title)
                .UseConverter(converter)
                .AddChoices(choices),
            cancellationToken);

        console.Markup(title.TrimEnd() + " ");
        console.Write(converter(value), Style.Plain);
        console.WriteLine();

        return value;
    }
}
