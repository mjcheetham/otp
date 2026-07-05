using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Mjcheetham.Otp.Interop;

/// <summary>
/// Minimal P/Invoke surface for libsecret (the Secret Service / freedesktop.org
/// keyring). Only the non-varargs <c>*v</c> entry points are used - they take a
/// <c>GHashTable</c> of attributes instead of a C varargs list, which is the
/// only form that is safe under source-generated (NativeAOT) marshalling.
/// </summary>
[SupportedOSPlatform("linux")]
internal static unsafe partial class Libsecret
{
    private const string Lib = "libsecret-1.so.0";

    // Item namespace. Must match NativeSecretOtpStore.ServiceName; used as the
    // schema name (stored as the "xdg:schema" attribute) to scope enumeration.
    internal const string SchemaName = "Mjcheetham.Otp";

    // SecretSearchFlags: ALL (1<<1) | UNLOCK (1<<2) | LOAD_SECRETS (1<<3).
    internal const int SearchAllUnlockLoad = 2 | 4 | 8;

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool secret_password_storev_sync(
        IntPtr schema, IntPtr attributes, string? collection, string label, string password,
        IntPtr cancellable, out IntPtr error);

    [LibraryImport(Lib)]
    internal static partial IntPtr secret_password_lookupv_sync(
        IntPtr schema, IntPtr attributes, IntPtr cancellable, out IntPtr error);

    [LibraryImport(Lib)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool secret_password_clearv_sync(
        IntPtr schema, IntPtr attributes, IntPtr cancellable, out IntPtr error);

    [LibraryImport(Lib)]
    internal static partial void secret_password_free(IntPtr password);

    [LibraryImport(Lib)]
    internal static partial IntPtr secret_service_search_sync(
        IntPtr service, IntPtr schema, IntPtr attributes, int flags, IntPtr cancellable, out IntPtr error);

    [LibraryImport(Lib)]
    internal static partial IntPtr secret_item_get_secret(IntPtr item);

    [LibraryImport(Lib)]
    internal static partial IntPtr secret_item_get_attributes(IntPtr item);

    [LibraryImport(Lib)]
    internal static partial IntPtr secret_value_get_text(IntPtr value);

    [LibraryImport(Lib)]
    internal static partial void secret_value_unref(IntPtr value);

    public static IntPtr BuildSchema(string name)
    {
        SecretSchema schema = default;
        schema.Name = Marshal.StringToCoTaskMemUTF8(name);
        schema.Flags = 0; // SECRET_SCHEMA_NONE
        schema.Attributes[0].Name = Marshal.StringToCoTaskMemUTF8("name");
        schema.Attributes[0].Type = 0; // SECRET_SCHEMA_ATTRIBUTE_STRING

        IntPtr memory = Marshal.AllocHGlobal(sizeof(SecretSchema));
        *(SecretSchema*)memory = schema;
        return memory;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SecretSchemaAttribute
    {
        public IntPtr Name;
        public int Type;
    }

    [InlineArray(32)]
    private struct SchemaAttributeArray
    {
        private SecretSchemaAttribute _element0;
    }

    // Mirrors the public SecretSchema layout: name, flags, a 32-entry attribute
    // array, then one reserved int and seven reserved pointers.
    [StructLayout(LayoutKind.Sequential)]
    private struct SecretSchema
    {
        public IntPtr Name;
        public int Flags;
        public SchemaAttributeArray Attributes;
        public int Reserved;
        public IntPtr Reserved1;
        public IntPtr Reserved2;
        public IntPtr Reserved3;
        public IntPtr Reserved4;
        public IntPtr Reserved5;
        public IntPtr Reserved6;
        public IntPtr Reserved7;
    }
}
