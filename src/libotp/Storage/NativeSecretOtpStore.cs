namespace Mjcheetham.Otp.Storage;

/// <summary>
/// Base class for <see cref="IOtpStore"/> implementations backed by an
/// operating-system secret vault (the macOS keychain, Windows Credential
/// Manager, or the Linux Secret Service via libsecret).
/// </summary>
/// <remarks>
/// <para>
/// Each account is persisted as a single vault item. The item's <em>key</em> is
/// the account <see cref="IOneTimePassword.Name"/> and its <em>secret</em> is
/// the account's <c>otpauth://</c> URI (see <see cref="OtpAuthUri"/>). The URI
/// already round-trips every field - name, issuer, digits, algorithm, and the
/// time step or counter - as well as the shared secret itself, so no additional
/// metadata storage is required and the value the user sees in their keychain
/// is a portable, standard authenticator URI.
/// </para>
/// <para>
/// Derived classes implement only the small set of platform primitives below;
/// this class owns serialization and the <see cref="IOtpStore"/> contract,
/// including the case-insensitive name semantics shared with
/// <see cref="FileOtpStore"/>.
/// </para>
/// </remarks>
public abstract class NativeSecretOtpStore : IOtpStore
{
    /// <summary>
    /// Namespace used to scope this application's items within the shared OS
    /// vault (keychain service, credential target prefix, or libsecret
    /// attribute) so enumeration never returns unrelated credentials.
    /// </summary>
    protected const string ServiceName = "Mjcheetham.Otp";

    private static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// Stores (adds or replaces) the secret <paramref name="payload"/> under the
    /// exact account <paramref name="name"/>. Implementations must upsert: a
    /// prior item with the same name is overwritten.
    /// </summary>
    protected abstract void StoreSecret(string name, string payload);

    /// <summary>
    /// Returns the secret payload stored under the exact (case-sensitive)
    /// account <paramref name="name"/>, or <see langword="null"/> if no such
    /// item exists.
    /// </summary>
    protected abstract string? LookupSecret(string name);

    /// <summary>
    /// Deletes the item stored under the exact (case-sensitive) account
    /// <paramref name="name"/>. Returns <see langword="false"/> if no such item
    /// exists.
    /// </summary>
    protected abstract bool ClearSecret(string name);

    /// <summary>
    /// Enumerates every item in this application's namespace as
    /// <c>(name, payload)</c> pairs. Implementations should materialize the
    /// result eagerly so any native handles are released before returning.
    /// </summary>
    protected abstract IEnumerable<(string Name, string Payload)> EnumerateEntries();

    public IAsyncEnumerable<IOneTimePassword> ListAsync(CancellationToken ct = default)
    {
        return EnumerateEntries()
            .Select(entry => OtpAuthUri.Parse(entry.Payload))
            .ToAsyncEnumerable();
    }

    public ValueTask<IOneTimePassword?> GetAsync(string name, CancellationToken ct = default)
    {
        // Fast path for the common case where the caller's casing matches the
        // stored key; fall back to a case-insensitive scan otherwise.
        string? payload = LookupSecret(name) ?? FindPayloadByName(name);
        IOneTimePassword? otp = payload is null ? null : OtpAuthUri.Parse(payload);
        return ValueTask.FromResult(otp);
    }

    public Task AddAsync(IOneTimePassword otp, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(otp);

        if (EnumerateEntries().Any(entry => NameComparer.Equals(entry.Name, otp.Name)))
        {
            throw new InvalidOperationException($"A one-time password named '{otp.Name}' already exists.");
        }

        StoreSecret(otp.Name, OtpAuthUri.Format(otp));
        return Task.CompletedTask;
    }

    public Task<bool> RemoveAsync(string name, CancellationToken ct = default)
    {
        if (ClearSecret(name))
        {
            return Task.FromResult(true);
        }

        // The caller's casing may differ from the stored key; resolve the exact
        // stored name and retry before reporting the item as missing.
        foreach ((string storedName, string _) in EnumerateEntries())
        {
            if (NameComparer.Equals(storedName, name))
            {
                return Task.FromResult(ClearSecret(storedName));
            }
        }

        return Task.FromResult(false);
    }

    private string? FindPayloadByName(string name)
    {
        foreach ((string storedName, string payload) in EnumerateEntries())
        {
            if (NameComparer.Equals(storedName, name))
            {
                return payload;
            }
        }

        return null;
    }
}
