using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Mjcheetham.Otp.Storage;

namespace Mjcheetham.Otp.Interop;

/// <summary>
/// Minimal P/Invoke surface for the Apple Security framework keychain
/// (<c>SecItem*</c>) APIs and the attribute-key constants used by
/// <see cref="MacOSKeychainOtpStore"/>.
/// </summary>
[SupportedOSPlatform("macos")]
internal static partial class SecurityFramework
{
    private const string Path = "/System/Library/Frameworks/Security.framework/Security";

    // Selected OSStatus result codes (SInt32).
    internal const int ErrSecSuccess = 0;
    internal const int ErrSecItemNotFound = -25300;
    internal const int ErrSecDuplicateItem = -25299;
    internal const int ErrSecAuthFailed = -25293;
    internal const int ErrSecUserCanceled = -128;

    private static readonly IntPtr s_handle = NativeLibrary.Load(Path);

    internal static readonly IntPtr ClassKey = Const("kSecClass");
    internal static readonly IntPtr ClassGenericPassword = Const("kSecClassGenericPassword");
    internal static readonly IntPtr AttrService = Const("kSecAttrService");
    internal static readonly IntPtr AttrAccount = Const("kSecAttrAccount");
    internal static readonly IntPtr AttrLabel = Const("kSecAttrLabel");
    internal static readonly IntPtr ValueData = Const("kSecValueData");
    internal static readonly IntPtr ReturnData = Const("kSecReturnData");
    internal static readonly IntPtr ReturnAttributes = Const("kSecReturnAttributes");
    internal static readonly IntPtr MatchLimit = Const("kSecMatchLimit");
    internal static readonly IntPtr MatchLimitAll = Const("kSecMatchLimitAll");

    [LibraryImport(Path)]
    internal static partial int SecItemAdd(IntPtr attributes, IntPtr result);

    [LibraryImport(Path)]
    internal static partial int SecItemCopyMatching(IntPtr query, out IntPtr result);

    [LibraryImport(Path)]
    internal static partial int SecItemUpdate(IntPtr query, IntPtr attributesToUpdate);

    [LibraryImport(Path)]
    internal static partial int SecItemDelete(IntPtr query);

    private static IntPtr Const(string name) => CoreFoundation.GetStringConstant(s_handle, name);
}
