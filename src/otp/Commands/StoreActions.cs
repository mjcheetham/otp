namespace Mjcheetham.Otp.Commands;

internal static class StoreActions
{
    /// <summary>
    /// Runs a store-backed command body, turning backend resolution or store
    /// failures (<see cref="OtpStoreException"/>) into a friendly error and a
    /// non-zero exit code. System.CommandLine's own handler would otherwise
    /// print the exception and stack trace.
    /// </summary>
    public static async Task<int> RunAsync(Func<Task<int>> body)
    {
        try
        {
            return await body();
        }
        catch (OtpStoreException ex)
        {
            Ui.ReportError(ex.Message);
            return 1;
        }
    }
}
