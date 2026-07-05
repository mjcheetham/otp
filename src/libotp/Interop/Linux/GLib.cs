using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Mjcheetham.Otp.Storage;

namespace Mjcheetham.Otp.Interop;

/// <summary>
/// Minimal P/Invoke surface for GLib/GObject, sufficient to build the attribute
/// hash tables, walk the <c>GList</c> search results, and read/free the
/// <c>GError</c> values used by <see cref="LinuxSecretServiceOtpStore"/>. Uses
/// source-generated <see cref="LibraryImportAttribute"/> marshalling to remain
/// NativeAOT-safe.
/// </summary>
[SupportedOSPlatform("linux")]
internal static partial class GLib
{
    private const string GLibLib = "libglib-2.0.so.0";
    private const string GObjectLib = "libgobject-2.0.so.0";

    private static readonly IntPtr s_glib = NativeLibrary.Load(GLibLib);
    private static readonly IntPtr s_gobject = NativeLibrary.Load(GObjectLib);

    // Function-pointer constants passed by value to g_hash_table_new /
    // g_list_free_full (the exported symbols are the functions themselves).
    internal static readonly IntPtr StrHash = NativeLibrary.GetExport(s_glib, "g_str_hash");
    internal static readonly IntPtr StrEqual = NativeLibrary.GetExport(s_glib, "g_str_equal");
    internal static readonly IntPtr ObjectUnref = NativeLibrary.GetExport(s_gobject, "g_object_unref");

    [LibraryImport(GLibLib)]
    internal static partial IntPtr g_hash_table_new(IntPtr hashFunc, IntPtr keyEqualFunc);

    [LibraryImport(GLibLib)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool g_hash_table_insert(IntPtr hashTable, IntPtr key, IntPtr value);

    [LibraryImport(GLibLib)]
    internal static partial IntPtr g_hash_table_lookup(IntPtr hashTable, IntPtr key);

    [LibraryImport(GLibLib)]
    internal static partial void g_hash_table_unref(IntPtr hashTable);

    [LibraryImport(GLibLib)]
    internal static partial void g_free(IntPtr mem);

    [LibraryImport(GLibLib)]
    internal static partial void g_error_free(IntPtr error);

    [LibraryImport(GLibLib)]
    internal static partial void g_list_free_full(IntPtr list, IntPtr freeFunc);

    /// <summary>Reads the <c>data</c> pointer of a <c>GList</c> node.</summary>
    internal static IntPtr ListData(IntPtr node) => Marshal.ReadIntPtr(node, 0);

    /// <summary>Reads the <c>next</c> pointer of a <c>GList</c> node.</summary>
    internal static IntPtr ListNext(IntPtr node) => Marshal.ReadIntPtr(node, IntPtr.Size);

    /// <summary>Reads the <c>code</c> and <c>message</c> from a <c>GError</c>.</summary>
    internal static (int Code, string? Message) ReadError(IntPtr error)
    {
        // GError { GQuark domain (guint32); gint code; gchar *message; }. The two
        // 4-byte fields place message at byte offset 8 on every ABI we target.
        int code = Marshal.ReadInt32(error, 4);
        IntPtr messagePtr = Marshal.ReadIntPtr(error, 8);
        return (code, Marshal.PtrToStringUTF8(messagePtr));
    }
}
