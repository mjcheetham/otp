using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Mjcheetham.Otp.Storage;

namespace Mjcheetham.Otp.Interop;

/// <summary>
/// Minimal P/Invoke surface for the Apple CoreFoundation framework, sufficient
/// to build the CFDictionary queries and read the CFData/CFString values used
/// by <see cref="MacOSKeychainOtpStore"/>. Uses source-generated
/// <see cref="LibraryImportAttribute"/> marshalling to remain NativeAOT-safe.
/// </summary>
[SupportedOSPlatform("macos")]
internal static unsafe partial class CoreFoundation
{
    private const string Path = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    // kCFStringEncodingUTF8. CoreFoundation string-encoding constant.
    private const uint Utf8Encoding = 0x08000100;

    private static readonly IntPtr s_handle = NativeLibrary.Load(Path);

    // The dictionary callback structs are exported by value, so the export
    // address *is* the pointer CFDictionaryCreate expects. The boolean constant
    // is an exported CFBooleanRef (a pointer variable), so it must be read once.
    internal static readonly IntPtr TypeDictionaryKeyCallBacks =
        NativeLibrary.GetExport(s_handle, "kCFTypeDictionaryKeyCallBacks");

    internal static readonly IntPtr TypeDictionaryValueCallBacks =
        NativeLibrary.GetExport(s_handle, "kCFTypeDictionaryValueCallBacks");

    internal static readonly IntPtr BooleanTrue =
        Marshal.ReadIntPtr(NativeLibrary.GetExport(s_handle, "kCFBooleanTrue"));

    [LibraryImport(Path)]
    internal static partial void CFRelease(IntPtr cf);

    [LibraryImport(Path)]
    private static partial IntPtr CFStringCreateWithBytes(
        IntPtr allocator, byte* bytes, nint numBytes, uint encoding, byte isExternalRepresentation);

    [LibraryImport(Path)]
    private static partial IntPtr CFDataCreate(IntPtr allocator, byte* bytes, nint length);

    [LibraryImport(Path)]
    private static partial nint CFDataGetLength(IntPtr data);

    [LibraryImport(Path)]
    private static partial IntPtr CFDataGetBytePtr(IntPtr data);

    [LibraryImport(Path)]
    private static partial IntPtr CFDictionaryCreate(
        IntPtr allocator, IntPtr* keys, IntPtr* values, nint numValues,
        IntPtr keyCallBacks, IntPtr valueCallBacks);

    [LibraryImport(Path)]
    internal static partial nint CFArrayGetCount(IntPtr array);

    [LibraryImport(Path)]
    internal static partial IntPtr CFArrayGetValueAtIndex(IntPtr array, nint index);

    [LibraryImport(Path)]
    internal static partial IntPtr CFDictionaryGetValue(IntPtr dictionary, IntPtr key);

    [LibraryImport(Path)]
    private static partial nint CFStringGetLength(IntPtr theString);

    [LibraryImport(Path)]
    private static partial nint CFStringGetMaximumSizeForEncoding(nint length, uint encoding);

    [LibraryImport(Path)]
    private static partial byte CFStringGetCString(IntPtr theString, byte* buffer, nint bufferSize, uint encoding);

    /// <summary>Resolves an exported CFStringRef constant (e.g. a <c>kSec*</c> key).</summary>
    internal static IntPtr GetStringConstant(IntPtr libraryHandle, string name) =>
        Marshal.ReadIntPtr(NativeLibrary.GetExport(libraryHandle, name));

    /// <summary>Creates an owned CFString from a managed string. Release with <see cref="CFRelease"/>.</summary>
    internal static IntPtr CreateString(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        fixed (byte* p = bytes)
        {
            return CFStringCreateWithBytes(IntPtr.Zero, p, bytes.Length, Utf8Encoding, 0);
        }
    }

    /// <summary>Creates an owned CFData from raw bytes. Release with <see cref="CFRelease"/>.</summary>
    internal static IntPtr CreateData(byte[] value)
    {
        fixed (byte* p = value)
        {
            return CFDataCreate(IntPtr.Zero, p, value.Length);
        }
    }

    /// <summary>Copies the bytes out of a CFData value (which the caller does not own).</summary>
    internal static byte[] GetDataBytes(IntPtr data)
    {
        int length = checked((int)CFDataGetLength(data));
        var buffer = new byte[length];
        if (length > 0)
        {
            Marshal.Copy(CFDataGetBytePtr(data), buffer, 0, length);
        }

        return buffer;
    }

    /// <summary>Reads a CFString value (which the caller does not own) as a managed string.</summary>
    internal static string GetString(IntPtr theString)
    {
        nint length = CFStringGetLength(theString);
        int capacity = checked((int)CFStringGetMaximumSizeForEncoding(length, Utf8Encoding)) + 1;
        var buffer = new byte[capacity];
        fixed (byte* p = buffer)
        {
            if (CFStringGetCString(theString, p, capacity, Utf8Encoding) == 0)
            {
                throw new OtpStoreException("Failed to read a keychain string attribute.");
            }

            return Marshal.PtrToStringUTF8((IntPtr)p) ?? string.Empty;
        }
    }

    /// <summary>
    /// Creates an owned CFDictionary with CoreFoundation type callbacks so the
    /// keys and values are retained for its lifetime. Release with
    /// <see cref="CFRelease"/>.
    /// </summary>
    internal static IntPtr CreateDictionary(IntPtr[] keys, IntPtr[] values)
    {
        fixed (IntPtr* k = keys)
        fixed (IntPtr* v = values)
        {
            return CFDictionaryCreate(
                IntPtr.Zero, k, v, keys.Length,
                TypeDictionaryKeyCallBacks, TypeDictionaryValueCallBacks);
        }
    }
}
