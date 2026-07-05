using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using static Mjcheetham.Otp.Interop.GLib;
using static Mjcheetham.Otp.Interop.Libsecret;

namespace Mjcheetham.Otp.Storage;

/// <summary>
/// An <see cref="IOtpStore"/> backed by the Linux Secret Service (the
/// freedesktop.org keyring, e.g. GNOME Keyring or KWallet's Secret Service
/// interface) via libsecret. Each account is stored under a single <c>name</c>
/// attribute, scoped by a schema shared with this application, and its secret
/// value is the account's <c>otpauth://</c> URI.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class LinuxSecretServiceOtpStore : NativeSecretOtpStore
{
    private static IntPtr s_schema;

    /// <summary>
    /// A process-lifetime <c>SecretSchema*</c> describing our single string
    /// attribute (<c>name</c>). Built once and intentionally never freed.
    /// </summary>
    private static IntPtr Schema => s_schema != IntPtr.Zero ? s_schema : s_schema = BuildSchema(ServiceName);

    protected override void StoreSecret(string name, string payload)
    {
        EnsureSupported();

        var allocations = new List<IntPtr>();
        IntPtr attributes = BuildAttributes(allocations, ("name", name));
        try
        {
            bool stored = secret_password_storev_sync(
                Schema, attributes, collection: null, label: $"OTP: {name}",
                password: payload, cancellable: IntPtr.Zero, out IntPtr error);

            if (!stored)
            {
                throw Error("write", error);
            }
        }
        finally
        {
            FreeAttributes(attributes, allocations);
        }
    }

    protected override string? LookupSecret(string name)
    {
        EnsureSupported();

        var allocations = new List<IntPtr>();
        IntPtr attributes = BuildAttributes(allocations, ("name", name));
        try
        {
            IntPtr result = secret_password_lookupv_sync(
                Schema, attributes, IntPtr.Zero, out IntPtr error);

            if (error != IntPtr.Zero)
            {
                throw Error("read", error);
            }

            if (result == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStringUTF8(result);
            }
            finally
            {
                secret_password_free(result);
            }
        }
        finally
        {
            FreeAttributes(attributes, allocations);
        }
    }

    protected override bool ClearSecret(string name)
    {
        EnsureSupported();

        var allocations = new List<IntPtr>();
        IntPtr attributes = BuildAttributes(allocations, ("name", name));
        try
        {
            bool removed = secret_password_clearv_sync(
                Schema, attributes, IntPtr.Zero, out IntPtr error);

            if (error != IntPtr.Zero)
            {
                throw Error("delete", error);
            }

            return removed;
        }
        finally
        {
            FreeAttributes(attributes, allocations);
        }
    }

    protected override IEnumerable<(string Name, string Payload)> EnumerateEntries()
    {
        EnsureSupported();

        var allocations = new List<IntPtr>();
        // An empty attribute set matches every item created under our schema.
        IntPtr attributes = BuildAttributes(allocations);
        IntPtr nameKey = Marshal.StringToCoTaskMemUTF8("name");
        try
        {
            IntPtr list = secret_service_search_sync(
                service: IntPtr.Zero, Schema, attributes,
                SearchAllUnlockLoad, IntPtr.Zero, out IntPtr error);

            if (error != IntPtr.Zero)
            {
                throw Error("enumerate", error);
            }

            if (list == IntPtr.Zero)
            {
                return [];
            }

            try
            {
                var entries = new List<(string, string)>();
                for (IntPtr node = list; node != IntPtr.Zero; node = ListNext(node))
                {
                    IntPtr item = ListData(node);
                    if (item == IntPtr.Zero)
                    {
                        continue;
                    }

                    string? payload = ReadSecret(item);
                    string? name = ReadName(item, nameKey);
                    if (payload is not null && name is not null)
                    {
                        entries.Add((name, payload));
                    }
                }

                return entries;
            }
            finally
            {
                g_list_free_full(list, ObjectUnref);
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(nameKey);
            FreeAttributes(attributes, allocations);
        }
    }

    private static string? ReadSecret(IntPtr item)
    {
        IntPtr value = secret_item_get_secret(item);
        if (value == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStringUTF8(secret_value_get_text(value));
        }
        finally
        {
            secret_value_unref(value);
        }
    }

    private static string? ReadName(IntPtr item, IntPtr nameKey)
    {
        IntPtr attributes = secret_item_get_attributes(item);
        if (attributes == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStringUTF8(g_hash_table_lookup(attributes, nameKey));
        }
        finally
        {
            g_hash_table_unref(attributes);
        }
    }

    private static IntPtr BuildAttributes(List<IntPtr> allocations, params (string Key, string Value)[] pairs)
    {
        IntPtr table = g_hash_table_new(StrHash, StrEqual);
        foreach ((string key, string value) in pairs)
        {
            IntPtr keyPtr = Marshal.StringToCoTaskMemUTF8(key);
            IntPtr valuePtr = Marshal.StringToCoTaskMemUTF8(value);
            allocations.Add(keyPtr);
            allocations.Add(valuePtr);
            g_hash_table_insert(table, keyPtr, valuePtr);
        }

        return table;
    }

    private static void FreeAttributes(IntPtr table, List<IntPtr> allocations)
    {
        // The table was created without key/value destroy functions, so free the
        // strings we allocated after releasing the table itself.
        if (table != IntPtr.Zero)
        {
            g_hash_table_unref(table);
        }

        foreach (IntPtr allocation in allocations)
        {
            Marshal.FreeCoTaskMem(allocation);
        }
    }

    private static void EnsureSupported()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException(
                "The Linux Secret Service store is only available on Linux.");
        }
    }

    private static OtpStoreException Error(string operation, IntPtr error)
    {
        (int code, string? message) = ReadError(error);
        g_error_free(error);
        return new OtpStoreException(
            $"Failed to {operation} secret (code {code}: {message ?? "unknown error"}).");
    }
}
