using System.Runtime.Versioning;
using System.Text;
using static Mjcheetham.Otp.Interop.CoreFoundation;
using static Mjcheetham.Otp.Interop.SecurityFramework;

namespace Mjcheetham.Otp.Storage;

/// <summary>
/// An <see cref="IOtpStore"/> backed by the macOS keychain. Each account is a
/// generic-password item scoped to the <see cref="NativeSecretOtpStore.ServiceName"/>
/// service, with its account name as the item account and its <c>otpauth://</c>
/// URI as the secret value.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacOSKeychainOtpStore : NativeSecretOtpStore
{
    protected override void StoreSecret(string name, string payload)
    {
        EnsureSupported();
        byte[] data = Encoding.UTF8.GetBytes(payload);

        using var scope = new CfScope();
        IntPtr service = scope.Track(CreateString(ServiceName));
        IntPtr account = scope.Track(CreateString(name));
        IntPtr value = scope.Track(CreateData(data));
        IntPtr label = scope.Track(CreateString($"OTP: {name}"));

        IntPtr attributes = scope.Track(CreateDictionary(
            [ClassKey, AttrService, AttrAccount, AttrLabel, ValueData],
            [ClassGenericPassword, service, account, label, value]));

        int status = SecItemAdd(attributes, IntPtr.Zero);
        if (status == ErrSecSuccess)
        {
            return;
        }

        if (status != ErrSecDuplicateItem)
        {
            throw Error("write", status);
        }

        // An item already exists for this account: replace its secret in place.
        IntPtr query = scope.Track(CreateDictionary(
            [ClassKey, AttrService, AttrAccount],
            [ClassGenericPassword, service, account]));
        IntPtr update = scope.Track(CreateDictionary([ValueData], [value]));

        status = SecItemUpdate(query, update);
        if (status != ErrSecSuccess)
        {
            throw Error("update", status);
        }
    }

    protected override string? LookupSecret(string name)
    {
        EnsureSupported();

        using var scope = new CfScope();
        IntPtr service = scope.Track(CreateString(ServiceName));
        IntPtr account = scope.Track(CreateString(name));

        IntPtr query = scope.Track(CreateDictionary(
            [ClassKey, AttrService, AttrAccount, ReturnData],
            [ClassGenericPassword, service, account, BooleanTrue]));

        int status = SecItemCopyMatching(query, out IntPtr result);
        if (status == ErrSecItemNotFound)
        {
            return null;
        }

        if (status != ErrSecSuccess)
        {
            throw Error("read", status);
        }

        scope.Track(result);
        return Encoding.UTF8.GetString(GetDataBytes(result));
    }

    protected override bool ClearSecret(string name)
    {
        EnsureSupported();

        using var scope = new CfScope();
        IntPtr service = scope.Track(CreateString(ServiceName));
        IntPtr account = scope.Track(CreateString(name));

        IntPtr query = scope.Track(CreateDictionary(
            [ClassKey, AttrService, AttrAccount],
            [ClassGenericPassword, service, account]));

        int status = SecItemDelete(query);
        if (status == ErrSecItemNotFound)
        {
            return false;
        }

        if (status != ErrSecSuccess)
        {
            throw Error("delete", status);
        }

        return true;
    }

    protected override IEnumerable<(string Name, string Payload)> EnumerateEntries()
    {
        EnsureSupported();

        using var scope = new CfScope();
        IntPtr service = scope.Track(CreateString(ServiceName));

        IntPtr query = scope.Track(CreateDictionary(
            [ClassKey, AttrService, MatchLimit, ReturnAttributes],
            [ClassGenericPassword, service, MatchLimitAll, BooleanTrue]));

        int status = SecItemCopyMatching(query, out IntPtr result);
        if (status == ErrSecItemNotFound || result == IntPtr.Zero)
        {
            return [];
        }

        if (status != ErrSecSuccess)
        {
            throw Error("enumerate", status);
        }

        scope.Track(result);

        // macOS rejects returning secret data for many items at once
        // (kSecMatchLimitAll + kSecReturnData => errSecParam), so enumerate the
        // account names from the attribute dictionaries and fetch each secret
        // individually. The name is authoritative from the account attribute.
        var entries = new List<(string, string)>();
        nint count = CFArrayGetCount(result);
        for (nint i = 0; i < count; i++)
        {
            IntPtr item = CFArrayGetValueAtIndex(result, i);
            IntPtr accountValue = CFDictionaryGetValue(item, AttrAccount);
            if (accountValue == IntPtr.Zero)
            {
                continue;
            }

            string name = GetString(accountValue);
            string? payload = LookupSecret(name);
            if (payload is not null)
            {
                entries.Add((name, payload));
            }
        }

        return entries;
    }

    private static void EnsureSupported()
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException(
                "The macOS keychain store is only available on macOS.");
        }
    }

    private static OtpStoreException Error(string operation, int status)
    {
        string detail = status switch
        {
            ErrSecUserCanceled => "the keychain request was cancelled",
            ErrSecAuthFailed => "keychain authentication failed",
            _ => $"OSStatus {status}"
        };

        return new OtpStoreException($"Failed to {operation} keychain item ({detail}).");
    }

    /// <summary>
    /// Tracks owned CoreFoundation objects and releases them (in reverse order)
    /// when disposed. CFDictionary retains its keys and values, so the objects
    /// added to a query can be released together once the call returns.
    /// </summary>
    private struct CfScope : IDisposable
    {
        private List<IntPtr>? _objects;

        public IntPtr Track(IntPtr cf)
        {
            if (cf != IntPtr.Zero)
            {
                (_objects ??= []).Add(cf);
            }

            return cf;
        }

        public void Dispose()
        {
            if (_objects is null)
            {
                return;
            }

            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                CFRelease(_objects[i]);
            }

            _objects = null;
        }
    }
}
