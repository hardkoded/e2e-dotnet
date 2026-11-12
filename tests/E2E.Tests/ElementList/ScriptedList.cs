// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.AttemptSession;
using Microsoft.Playwright;

namespace E2E.Tests.ElementList;

/// <summary>One element handle the scripted array made, and how often it was released.</summary>
internal sealed class MadeElement
{
    private int _disposed;

    public int Index { get; init; }

    public IJSHandle Handle { get; init; } = null!;

    public int Disposed => Volatile.Read(ref _disposed);

    public void CountDispose() => Interlocked.Increment(ref _disposed);
}

/// <summary>
/// A scripted array handle whose <c>EvaluateHandleAsync</c> answers with one element handle per call (upstream's <c>scripted</c>
/// helper in <c>element-list.test.ts</c>).
/// </summary>
internal sealed class ScriptedList
{
    private readonly List<MadeElement> _made = [];
    private int _disposed;
    private int _evaluated;

    public ScriptedList(bool missing = false, Task? gate = null, Exception? firstFailure = null)
    {
        var (handle, fake) = Fake<IJSHandle>.New();
        Handle = handle;
        var failure = firstFailure;
        fake.Members["DisposeAsync"] = _ =>
        {
            Interlocked.Increment(ref _disposed);
            return ValueTask.CompletedTask;
        };
        fake.Members["EvaluateHandleAsync"] = args =>
        {
            Interlocked.Increment(ref _evaluated);
            var index = (int)args[1]!;
            return Interlocked.Exchange(ref failure, null) is { } thrown
                ? Task.FromException<IJSHandle>(thrown)
                : EvaluateAsync(index);
        };

        async Task<IJSHandle> EvaluateAsync(int index)
        {
            if (gate is not null)
            {
                await gate.ConfigureAwait(false);
            }

            MadeElement? made = null;
            if (missing)
            {
                // A handle to a missing element is a plain JSHandle, not an element handle.
                var (plain, plainFake) = Fake<IJSHandle>.New();
                plainFake.Members["DisposeAsync"] = _ =>
                {
                    made!.CountDispose();
                    return ValueTask.CompletedTask;
                };
                made = new MadeElement { Index = index, Handle = plain };
            }
            else
            {
                var (element, elementFake) = Fake<IElementHandle>.New();
                elementFake.Members["DisposeAsync"] = _ =>
                {
                    made!.CountDispose();
                    return ValueTask.CompletedTask;
                };
                made = new MadeElement { Index = index, Handle = element };
            }

            lock (_made)
            {
                _made.Add(made);
            }

            return made.Handle;
        }
    }

    public IJSHandle Handle { get; }

    /// <summary>How often the array handle was released.</summary>
    public int Disposed => Volatile.Read(ref _disposed);

    /// <summary>How many element handles the page was asked for.</summary>
    public int Evaluated => Volatile.Read(ref _evaluated);

    public IReadOnlyList<MadeElement> Made
    {
        get
        {
            lock (_made)
            {
                return [.. _made];
            }
        }
    }
}
