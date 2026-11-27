// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;

namespace E2E.Desktop.MacOs;

/// <summary>
/// A thin layer over the Objective-C runtime for the few AppKit and Foundation calls the driver makes (<c>NSWorkspace</c>,
/// <c>NSRunningApplication</c>, <c>NSDictionary</c>), where the Rust driver uses the <c>objc2</c> crates. A message is sent with
/// <c>objc_msgSend</c>, whose C prototype is cast per call shape by the overloads of <see cref="Native"/>. An <c>NSString</c> is
/// toll-free bridged with a CFString, so the CoreFoundation helpers read and create them. .NET-only helper.
/// </summary>
internal static unsafe class ObjC
{
    private static readonly ConcurrentDictionary<string, nint> s_selectors = new();
    private static readonly ConcurrentDictionary<string, nint> s_classes = new();

    static ObjC()
    {
        // A class lives in a framework that has to be loaded before the runtime knows it.
        Cf.Load(Native.FoundationPath);
        Cf.Load(Native.AppKitPath);
    }

    /// <summary>Runs <paramref name="work"/> in an autorelease pool: a pool thread has none, and the AppKit and Foundation calls a launch makes would otherwise leak what they autorelease for as long as the thread lives.</summary>
    public static T InAutoreleasePool<T>(Func<T> work)
    {
        var pool = Native.objc_autoreleasePoolPush();
        try
        {
            return work();
        }
        finally
        {
            Native.objc_autoreleasePoolPop(pool);
        }
    }

    public static nint Class(string name) => s_classes.GetOrAdd(name, static key =>
        {
            fixed (byte* bytes = Cf.Utf8Z(key))
            {
                return Native.objc_getClass(bytes);
            }
        });

    private static nint Selector(string name) => s_selectors.GetOrAdd(name, static key =>
        {
            fixed (byte* bytes = Cf.Utf8Z(key))
            {
                return Native.sel_registerName(bytes);
            }
        });

    /// <summary>Sends a message with no argument, answering an object (0 for nil).</summary>
    public static nint Send(nint receiver, string selector) => Native.objc_msgSend(receiver, Selector(selector));

    /// <summary>Sends a message with one object argument, answering an object (0 for nil).</summary>
    public static nint Send(nint receiver, string selector, nint argument) =>
        Native.objc_msgSend(receiver, Selector(selector), argument);

    /// <summary>Sends a message with one <c>pid_t</c> argument, answering an object (0 for nil).</summary>
    public static nint SendPid(nint receiver, string selector, int pid) =>
        Native.objc_msgSend_Pid(receiver, Selector(selector), pid);

    /// <summary>Sends a message with no argument, answering a 32-bit integer.</summary>
    public static int SendInt(nint receiver, string selector) => Native.objc_msgSend_Int(receiver, Selector(selector));

    /// <summary>Runs <paramref name="use"/> with a temporary NSString (a CFString) of <paramref name="text"/>, released afterward.</summary>
    public static T WithString<T>(string text, Func<nint, T> use)
    {
        var value = Cf.CreateString(text);
        try
        {
            return use(value);
        }
        finally
        {
            Native.CFRelease(value);
        }
    }
}
