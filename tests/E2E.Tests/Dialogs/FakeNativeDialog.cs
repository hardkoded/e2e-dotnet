// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Playwright;

namespace E2E.Tests.Dialogs;

/// <summary>Upstream's <c>fakeDialog()</c> (<c>packages/web/tests/unit/dialogs.test.ts</c>): a native dialog that counts its answers.</summary>
internal sealed class FakeNativeDialog(string message = "are you sure?", string type = "confirm") : IDialog
{
    public int Accepts { get; private set; }

    public int Dismisses { get; private set; }

    public string? AcceptedText { get; private set; }

    /// <summary>Rejects the next <see cref="AcceptAsync"/> once (upstream's <c>accept.mockRejectedValueOnce</c>).</summary>
    public Exception? AcceptFailure { get; set; }

    public string DefaultValue => "";

    public string Message => message;

    public IPage Page => throw new NotSupportedException();

    public string Type => type;

    public Task AcceptAsync(string? promptText = null)
    {
        Accepts++;
        AcceptedText = promptText;
        if (AcceptFailure is { } failure)
        {
            AcceptFailure = null;
            return Task.FromException(failure);
        }

        return Task.CompletedTask;
    }

    public Task DismissAsync()
    {
        Dismisses++;
        return Task.CompletedTask;
    }
}
