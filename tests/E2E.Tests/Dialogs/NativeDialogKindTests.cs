// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.Dialogs;

/// <summary>Upstream's <c>describe('native dialog kind')</c> (<c>packages/web/tests/unit/dialogs.test.ts</c>).</summary>
public sealed class NativeDialogKindTests
{
    [Theory]
    [InlineData("alert", DialogType.Alert)]
    [InlineData("confirm", DialogType.Confirm)]
    [InlineData("prompt", DialogType.Prompt)]
    [InlineData("beforeunload", DialogType.BeforeUnload)]
    public async Task Exposes_the_kind_independently_of_identical_messages(string kind, DialogType expected)
    {
        var router = new DialogRouter();
        var native = new FakeNativeDialog("same message", kind);
        var seen = new List<DialogType>();
        router.Add((DialogHandler)(async dialog =>
        {
            seen.Add(dialog.Type);
            await dialog.DismissAsync();
        }));
        await router.DispatchAsync(native);
        Assert.Equal([expected], seen);
        Assert.Equal(1, native.Dismisses);
        router.ThrowPending();
    }
}
