// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace E2E.Tests.Locators;

/// <summary>
/// A chain-recording stand-in for Playwright's page, frame locator, and locator (upstream's <c>fakeLocator</c> in <c>locators.test.ts</c>):
/// every call appends one step to the chain the result carries, so a test reads the chain a projection composed. Test-only. Upstream's fake
/// is an object literal; a .NET interface needs a <see cref="DispatchProxy"/>.
/// </summary>
public class ChainRecording : DispatchProxy
{
    private IReadOnlyList<string> _chain = [];

    /// <summary>The page the chain starts from.</summary>
    public static IPage Page() => Make<IPage>([]);

    /// <summary>The steps a locator or frame locator recorded so far.</summary>
    public static IReadOnlyList<string> ChainOf(object recorded) => ((ChainRecording)recorded)._chain;

    private static T Make<T>(IReadOnlyList<string> chain)
        where T : class
    {
        var proxy = Create<T, ChainRecording>();
        ((ChainRecording)(object)proxy)._chain = chain;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        args ??= [];
        switch (targetMethod.Name)
        {
            case "Locator":
                return Make<ILocator>([.. _chain, $"locator({args[0]})"]);
            case "FrameLocator":
                return Make<IFrameLocator>([.. _chain, $"frameLocator({args[0]})"]);
            case "GetByLabel":
                return Make<ILocator>([.. _chain, $"label({Text(args[0])}{(Exact(args[1]) ? ",exact" : string.Empty)})"]);
            case "GetByText":
                return Make<ILocator>([.. _chain, $"text({Text(args[0])}{(Exact(args[1]) ? ",exact" : string.Empty)})"]);
            case "GetByPlaceholder":
                return Make<ILocator>([.. _chain, $"placeholder({Text(args[0])}{(Exact(args[1]) ? ",exact" : string.Empty)})"]);
            case "Filter":
                return Make<ILocator>([.. _chain, Filter((LocatorFilterOptions?)args[0])]);
            case "get_First":
                return Make<ILocator>([.. _chain, "first"]);
            case "get_Last":
                return Make<ILocator>([.. _chain, "last"]);
            case "Nth":
                return Make<ILocator>([.. _chain, $"nth({args[0]})"]);
            case "Or":
                return Make<ILocator>([.. _chain, $"or({string.Join('>', ChainOf(args[0]!))})"]);
            default:
                throw new NotSupportedException(targetMethod.Name + " is not recorded");
        }
    }

    // A string as it is, a RegExp as upstream's String(re) writes it.
    private static string Text(object? text) => text is Regex regex ? "/" + regex + "/" + Flags(regex) : (string)text!;

    private static string Flags(Regex regex) =>
        ((regex.Options & RegexOptions.IgnoreCase) != 0 ? "i" : string.Empty)
        + ((regex.Options & RegexOptions.Multiline) != 0 ? "m" : string.Empty)
        + ((regex.Options & RegexOptions.Singleline) != 0 ? "s" : string.Empty);

    private static bool Exact(object? options) => options switch
    {
        LocatorGetByTextOptions { Exact: true } => true,
        LocatorGetByLabelOptions { Exact: true } => true,
        LocatorGetByPlaceholderOptions { Exact: true } => true,
        _ => false,
    };

    private static string Filter(LocatorFilterOptions? options)
    {
        options ??= new LocatorFilterOptions();
        string[] parts =
        [
            options.HasText is null ? string.Empty : $"hasText={options.HasText}",
            options.HasTextRegex is null ? string.Empty : $"hasText={Text(options.HasTextRegex)}",
            options.HasNotText is null ? string.Empty : $"hasNotText={options.HasNotText}",
            options.HasNotTextRegex is null ? string.Empty : $"hasNotText={Text(options.HasNotTextRegex)}",
            options.Has is null ? string.Empty : $"has={string.Join('>', ChainOf(options.Has))}",
            options.HasNot is null ? string.Empty : $"hasNot={string.Join('>', ChainOf(options.HasNot))}",
            options.Visible is null ? string.Empty : $"visible={options.Visible.ToString()!.ToLowerInvariant()}",
        ];
        return $"filter({string.Join(',', parts.Where(part => part != string.Empty))})";
    }
}
