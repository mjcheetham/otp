using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Windows.Win32.Security.Credentials;
using static Windows.Win32.PInvoke;

// This whole type wraps the Windows Credential Manager. CsWin32 annotates the
// Cred* APIs as [SupportedOSPlatform("windows5.1.2600")]; the class-level
// [SupportedOSPlatform("windows")] already constrains external callers, and
// every .NET-supported Windows release is far newer than that XP-era metadata
// floor, so the version-gap CA1416 warnings here are not actionable.
#pragma warning disable CA1416

namespace Mjcheetham.Otp.Storage;

/// <summary>
/// An <see cref="IOtpStore"/> backed by the Windows Credential Manager. Each
/// account is a generic credential whose target name is
/// <c>Mjcheetham.Otp:{name}</c> and whose credential blob is the account's
/// <c>otpauth://</c> URI (UTF-8). Credentials are persisted with
/// <see cref="CRED_PERSIST.CRED_PERSIST_LOCAL_MACHINE"/> so they are available
/// across logon sessions on the machine but never roam.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsCredentialOtpStore : NativeSecretOtpStore
{
    private const string TargetPrefix = ServiceName + ":";
    private const string EnumerateFilter = TargetPrefix + "*";

    // winerror.h ERROR_NOT_FOUND.
    private const int ErrorNotFound = 1168;

    protected override unsafe void StoreSecret(string name, string payload)
    {
        EnsureSupported();

        string target = TargetPrefix + name;
        byte[] blob = Encoding.UTF8.GetBytes(payload);

        fixed (char* targetPtr = target)
        fixed (char* userPtr = name)
        fixed (byte* blobPtr = blob)
        {
            var credential = new CREDENTIALW
            {
                Type = CRED_TYPE.CRED_TYPE_GENERIC,
                TargetName = targetPtr,
                UserName = userPtr,
                CredentialBlob = blobPtr,
                CredentialBlobSize = (uint)blob.Length,
                Persist = CRED_PERSIST.CRED_PERSIST_LOCAL_MACHINE
            };

            if (!CredWrite(credential, 0))
            {
                throw Error("write", Marshal.GetLastPInvokeError());
            }
        }
    }

    protected override unsafe string? LookupSecret(string name)
    {
        EnsureSupported();

        if (!CredRead(TargetPrefix + name, CRED_TYPE.CRED_TYPE_GENERIC, out CREDENTIALW* credential))
        {
            int error = Marshal.GetLastPInvokeError();
            return error == ErrorNotFound ? null : throw Error("read", error);
        }

        try
        {
            return ReadBlob(credential);
        }
        finally
        {
            CredFree(credential);
        }
    }

    protected override unsafe bool ClearSecret(string name)
    {
        EnsureSupported();

        if (!CredDelete(TargetPrefix + name, CRED_TYPE.CRED_TYPE_GENERIC))
        {
            int error = Marshal.GetLastPInvokeError();
            return error == ErrorNotFound ? false : throw Error("delete", error);
        }

        return true;
    }

    protected override unsafe IEnumerable<(string Name, string Payload)> EnumerateEntries()
    {
        EnsureSupported();

        if (!CredEnumerate(EnumerateFilter, out uint count, out CREDENTIALW** credentials))
        {
            int error = Marshal.GetLastPInvokeError();
            if (error == ErrorNotFound)
            {
                return [];
            }

            throw Error("enumerate", error);
        }

        try
        {
            var entries = new List<(string, string)>((int)count);
            for (uint i = 0; i < count; i++)
            {
                CREDENTIALW* credential = credentials[i];
                string? target = credential->TargetName.ToString();
                if (string.IsNullOrEmpty(target))
                {
                    continue;
                }

                string name = target.StartsWith(TargetPrefix, StringComparison.Ordinal)
                    ? target[TargetPrefix.Length..]
                    : target;
                entries.Add((name, ReadBlob(credential)));
            }

            return entries;
        }
        finally
        {
            CredFree(credentials);
        }
    }

    private static unsafe string ReadBlob(CREDENTIALW* credential)
    {
        if (credential->CredentialBlob is null || credential->CredentialBlobSize == 0)
        {
            return string.Empty;
        }

        return Encoding.UTF8.GetString(credential->CredentialBlob, checked((int)credential->CredentialBlobSize));
    }

    private static void EnsureSupported()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "The Windows Credential Manager store is only available on Windows.");
        }
    }

    private static OtpStoreException Error(string operation, int error)
    {
        var inner = new Win32Exception(error);
        return new OtpStoreException(
            $"Failed to {operation} Windows credential (error {error}: {inner.Message}).", inner);
    }
}

#pragma warning restore CA1416
