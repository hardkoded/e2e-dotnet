// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;

namespace E2E.Desktop.MacOs;

/// <summary>A <c>CGPoint</c>: two doubles, in screen points.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct CGPoint(double x, double y)
{
    public double X = x;
    public double Y = y;
}

/// <summary>A <c>CGSize</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct CGSize
{
    public double Width;
    public double Height;
}

/// <summary>A <c>CGRect</c>: an origin and a size, passed by value to the functions that take one.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct CGRect(double x, double y, double width, double height)
{
    public double X = x;
    public double Y = y;
    public double Width = width;
    public double Height = height;
}

/// <summary>A <c>CFRange</c>: <c>CFIndex</c> is a pointer-sized signed integer.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct CFRange(nint location, nint length)
{
    public nint Location = location;
    public nint Length = length;
}

/// <summary>
/// Every native function the driver calls, by library. Each signature is the C prototype of the named function in the system
/// frameworks, with <c>CFTypeRef</c> and the other object references as <see cref="nint"/>, C <c>Boolean</c> and <c>bool</c> as
/// <see cref="byte"/>, and <c>CFIndex</c> as <see cref="nint"/>. The Rust crate declares the same functions with <c>extern "C"</c>.
/// </summary>
internal static unsafe partial class Native
{
    public const string CoreFoundationPath = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    public const string ApplicationServicesPath =
        "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";
    public const string CoreGraphicsPath = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    public const string CarbonPath = "/System/Library/Frameworks/Carbon.framework/Carbon";
    public const string AppKitPath = "/System/Library/Frameworks/AppKit.framework/AppKit";
    public const string FoundationPath = "/System/Library/Frameworks/Foundation.framework/Foundation";
    public const string LibSystem = "/usr/lib/libSystem.B.dylib";
    public const string ObjCPath = "/usr/lib/libobjc.A.dylib";

    // ---- CoreFoundation ----

    [LibraryImport(CoreFoundationPath)]
    public static partial nint CFRetain(nint cf);

    [LibraryImport(CoreFoundationPath)]
    public static partial void CFRelease(nint cf);

    [LibraryImport(CoreFoundationPath)]
    public static partial byte CFEqual(nint a, nint b);

    [LibraryImport(CoreFoundationPath)]
    public static partial nuint CFHash(nint cf);

    [LibraryImport(CoreFoundationPath)]
    public static partial nuint CFGetTypeID(nint cf);

    [LibraryImport(CoreFoundationPath)]
    public static partial nuint CFStringGetTypeID();

    [LibraryImport(CoreFoundationPath)]
    public static partial nuint CFBooleanGetTypeID();

    [LibraryImport(CoreFoundationPath)]
    public static partial nuint CFNumberGetTypeID();

    [LibraryImport(CoreFoundationPath)]
    public static partial nuint CFArrayGetTypeID();

    /// <summary><c>CFStringCreateWithCharacters(allocator, const UniChar *chars, CFIndex count)</c>: the string of exactly these UTF-16 units.</summary>
    [LibraryImport(CoreFoundationPath)]
    public static partial nint CFStringCreateWithCharacters(nint allocator, char* chars, nint count);

    [LibraryImport(CoreFoundationPath)]
    public static partial nint CFStringGetLength(nint theString);

    /// <summary><c>CFStringGetCharacters(theString, CFRange range, UniChar *buffer)</c>.</summary>
    [LibraryImport(CoreFoundationPath)]
    public static partial void CFStringGetCharacters(nint theString, CFRange range, char* buffer);

    /// <summary><c>CFArrayCreate(allocator, const void **values, CFIndex numValues, const CFArrayCallBacks *callBacks)</c>.</summary>
    [LibraryImport(CoreFoundationPath)]
    public static partial nint CFArrayCreate(nint allocator, nint* values, nint numValues, nint callBacks);

    [LibraryImport(CoreFoundationPath)]
    public static partial nint CFArrayGetCount(nint theArray);

    [LibraryImport(CoreFoundationPath)]
    public static partial nint CFArrayGetValueAtIndex(nint theArray, nint index);

    [LibraryImport(CoreFoundationPath)]
    public static partial byte CFBooleanGetValue(nint boolean);

    /// <summary><c>CFNumberGetValue(number, CFNumberType type, void *valuePtr)</c>.</summary>
    [LibraryImport(CoreFoundationPath)]
    public static partial byte CFNumberGetValue(nint number, int type, void* valuePtr);

    /// <summary><c>CFNumberCreate(allocator, CFNumberType type, const void *valuePtr)</c>.</summary>
    [LibraryImport(CoreFoundationPath)]
    public static partial nint CFNumberCreate(nint allocator, int type, void* valuePtr);

    [LibraryImport(CoreFoundationPath)]
    public static partial nint CFDictionaryGetValue(nint theDict, nint key);

    [LibraryImport(CoreFoundationPath)]
    public static partial byte* CFDataGetBytePtr(nint theData);

    // ---- ApplicationServices: the accessibility API ----

    [LibraryImport(ApplicationServicesPath)]
    public static partial nint AXUIElementCreateApplication(int pid);

    [LibraryImport(ApplicationServicesPath)]
    public static partial nint AXUIElementCreateSystemWide();

    [LibraryImport(ApplicationServicesPath)]
    public static partial int AXUIElementCopyAttributeValue(nint element, nint attribute, nint* value);

    [LibraryImport(ApplicationServicesPath)]
    public static partial int AXUIElementCopyAttributeValues(
        nint element,
        nint attribute,
        nint index,
        nint maxValues,
        nint* values
    );

    [LibraryImport(ApplicationServicesPath)]
    public static partial int AXUIElementGetAttributeValueCount(nint element, nint attribute, nint* count);

    /// <summary><c>AXUIElementCopyMultipleAttributeValues(element, CFArrayRef attributes, AXCopyMultipleAttributeOptions options, CFArrayRef *values)</c>.</summary>
    [LibraryImport(ApplicationServicesPath)]
    public static partial int AXUIElementCopyMultipleAttributeValues(
        nint element,
        nint attributes,
        uint options,
        nint* values
    );

    [LibraryImport(ApplicationServicesPath)]
    public static partial int AXUIElementSetAttributeValue(nint element, nint attribute, nint value);

    [LibraryImport(ApplicationServicesPath)]
    public static partial int AXUIElementIsAttributeSettable(nint element, nint attribute, byte* settable);

    [LibraryImport(ApplicationServicesPath)]
    public static partial int AXUIElementPerformAction(nint element, nint action);

    [LibraryImport(ApplicationServicesPath)]
    public static partial int AXUIElementCopyActionNames(nint element, nint* names);

    [LibraryImport(ApplicationServicesPath)]
    public static partial int AXUIElementSetMessagingTimeout(nint element, float timeoutInSeconds);

    [LibraryImport(ApplicationServicesPath)]
    public static partial int AXUIElementCopyElementAtPosition(nint application, float x, float y, nint* element);

    [LibraryImport(ApplicationServicesPath)]
    public static partial int AXUIElementGetPid(nint element, int* pid);

    [LibraryImport(ApplicationServicesPath)]
    public static partial nuint AXUIElementGetTypeID();

    [LibraryImport(ApplicationServicesPath)]
    public static partial nuint AXValueGetTypeID();

    [LibraryImport(ApplicationServicesPath)]
    public static partial uint AXValueGetType(nint value);

    [LibraryImport(ApplicationServicesPath)]
    public static partial byte AXValueGetValue(nint value, uint type, void* valuePtr);

    [LibraryImport(ApplicationServicesPath)]
    public static partial nint AXValueCreate(uint type, void* valuePtr);

    [LibraryImport(ApplicationServicesPath)]
    public static partial byte AXIsProcessTrusted();

    /// <summary>Private, and stable since macOS 10.6: the window server's id of a window element.</summary>
    [LibraryImport(ApplicationServicesPath)]
    public static partial int _AXUIElementGetWindow(nint element, uint* identifier);

    // ---- CoreGraphics: the window server, display, and events ----

    [LibraryImport(CoreGraphicsPath)]
    public static partial byte CGPreflightScreenCaptureAccess();

    [LibraryImport(CoreGraphicsPath)]
    public static partial nint CGSessionCopyCurrentDictionary();

    [LibraryImport(CoreGraphicsPath)]
    public static partial int CGGetDisplaysWithRect(
        CGRect rect,
        uint maxDisplays,
        uint* displays,
        uint* matchingDisplayCount
    );

    [LibraryImport(CoreGraphicsPath)]
    public static partial nint CGDisplayCopyDisplayMode(uint display);

    [LibraryImport(CoreGraphicsPath)]
    public static partial nuint CGDisplayModeGetPixelWidth(nint mode);

    [LibraryImport(CoreGraphicsPath)]
    public static partial nuint CGDisplayModeGetWidth(nint mode);

    [LibraryImport(CoreGraphicsPath)]
    public static partial void CGDisplayModeRelease(nint mode);

    /// <summary><c>CGWindowListCopyWindowInfo(CGWindowListOption option, CGWindowID relativeToWindow)</c>: an array of window dictionaries, owned by the caller.</summary>
    [LibraryImport(CoreGraphicsPath)]
    public static partial nint CGWindowListCopyWindowInfo(uint option, uint relativeToWindow);

    [LibraryImport(CoreGraphicsPath)]
    public static partial byte CGRectMakeWithDictionaryRepresentation(nint dict, CGRect* rect);

    [LibraryImport(CoreGraphicsPath)]
    public static partial nint CGEventSourceCreate(int stateID);

    [LibraryImport(CoreGraphicsPath)]
    public static partial nint CGEventCreateKeyboardEvent(nint source, ushort virtualKey, byte keyDown);

    [LibraryImport(CoreGraphicsPath)]
    public static partial nint CGEventCreateMouseEvent(
        nint source,
        uint mouseType,
        CGPoint mouseCursorPosition,
        uint mouseButton
    );

    [LibraryImport(CoreGraphicsPath)]
    public static partial void CGEventSetFlags(nint eventRef, ulong flags);

    /// <summary><c>CGEventKeyboardSetUnicodeString(event, UniCharCount stringLength, const UniChar *unicodeString)</c>.</summary>
    [LibraryImport(CoreGraphicsPath)]
    public static partial void CGEventKeyboardSetUnicodeString(nint eventRef, nuint stringLength, char* unicodeString);

    [LibraryImport(CoreGraphicsPath)]
    public static partial void CGEventSetIntegerValueField(nint eventRef, uint field, long value);

    [LibraryImport(CoreGraphicsPath)]
    public static partial void CGEventPost(uint tap, nint eventRef);

    [LibraryImport(CoreGraphicsPath)]
    public static partial void CGEventPostToPid(int pid, nint eventRef);

    // ---- Carbon: the keyboard layout ----

    [LibraryImport(CarbonPath)]
    public static partial nint TISCopyCurrentASCIICapableKeyboardLayoutInputSource();

    [LibraryImport(CarbonPath)]
    public static partial nint TISGetInputSourceProperty(nint inputSource, nint propertyKey);

    [LibraryImport(CarbonPath)]
    public static partial byte LMGetKbdType();

    /// <summary><c>UCKeyTranslate(const UCKeyboardLayout *layout, UInt16 virtualKeyCode, UInt16 keyAction, UInt32 modifierKeyState, UInt32 keyboardType, OptionBits keyTranslateOptions, UInt32 *deadKeyState, UniCharCount maxStringLength, UniCharCount *actualStringLength, UniChar *unicodeString)</c>.</summary>
    [LibraryImport(CarbonPath)]
    public static partial int UCKeyTranslate(
        void* layout,
        ushort virtualKeyCode,
        ushort keyAction,
        uint modifierKeyState,
        uint keyboardType,
        uint keyTranslateOptions,
        uint* deadKeyState,
        nuint maxStringLength,
        nuint* actualStringLength,
        ushort* unicodeString
    );

    // ---- libSystem ----

    [LibraryImport(LibSystem, SetLastError = true)]
    public static partial int kill(int pid, int signal);

    [LibraryImport(LibSystem)]
    public static partial int proc_pidinfo(int pid, int flavor, ulong arg, void* buffer, int bufferSize);

    [LibraryImport(LibSystem)]
    public static partial int proc_listallpids(void* buffer, int bufferSize);

    [LibraryImport(LibSystem)]
    public static partial int sysctl(int* name, uint nameLength, void* oldp, nuint* oldlenp, void* newp, nuint newlen);

    [LibraryImport(LibSystem)]
    public static partial int getpid();

    // ---- The Objective-C runtime ----

    [LibraryImport(ObjCPath)]
    public static partial nint objc_getClass(byte* name);

    [LibraryImport(ObjCPath)]
    public static partial nint sel_registerName(byte* name);

    [LibraryImport(ObjCPath)]
    public static partial nint objc_autoreleasePoolPush();

    [LibraryImport(ObjCPath)]
    public static partial void objc_autoreleasePoolPop(nint pool);

    /// <summary>A message with no argument, answering an object.</summary>
    [LibraryImport(ObjCPath, EntryPoint = "objc_msgSend")]
    public static partial nint objc_msgSend(nint receiver, nint selector);

    /// <summary>A message with an object argument, answering an object.</summary>
    [LibraryImport(ObjCPath, EntryPoint = "objc_msgSend")]
    public static partial nint objc_msgSend(nint receiver, nint selector, nint argument);

    /// <summary>A message with a <c>pid_t</c> argument, answering an object.</summary>
    [LibraryImport(ObjCPath, EntryPoint = "objc_msgSend")]
    public static partial nint objc_msgSend_Pid(nint receiver, nint selector, int pid);

    /// <summary>A message with no argument, answering a 32-bit integer (<c>processIdentifier</c>): its own entry, as the upper half of a wider return is undefined.</summary>
    [LibraryImport(ObjCPath, EntryPoint = "objc_msgSend")]
    public static partial int objc_msgSend_Int(nint receiver, nint selector);
}
