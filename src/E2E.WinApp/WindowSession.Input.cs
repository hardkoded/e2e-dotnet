// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.WinApp;

public sealed partial class WindowSession
{
    /// <summary>A pointer hover over a node.</summary>
    public Task HoverAsync(SemanticNode node, CancellationToken cancellationToken = default) =>
        WindowActions.HoverAsync(this, node, cancellationToken);

    /// <summary>A drag from one node to another.</summary>
    public Task DragToAsync(SemanticNode node, SemanticNode destination, CancellationToken cancellationToken = default) =>
        WindowActions.DragToAsync(this, node, destination, cancellationToken);

    /// <summary>A press held on a node. Without a duration it holds <see cref="WindowActions.DefaultLongPress"/>.</summary>
    public Task LongPressAsync(SemanticNode node, TimeSpan? duration = null, CancellationToken cancellationToken = default) =>
        WindowActions.LongPressAsync(this, node, duration, cancellationToken);

    /// <summary>A right click on a node: what opens a context menu.</summary>
    public Task SecondaryTapAsync(SemanticNode node, CancellationToken cancellationToken = default) =>
        WindowActions.SecondaryTapAsync(this, node, cancellationToken);

    /// <summary>Types into whatever holds focus, as literal text. <paramref name="replace"/> selects the field's content first.</summary>
    public async Task TypeTextAsync(string text, bool replace = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        var window = MainWindow;
        await CommandAsync(
            "keyboard.type",
            async (driver, token) =>
            {
                // Keys go through SendInput, which reaches the foreground window only.
                await driver.ForegroundAsync(window, token).ConfigureAwait(false);
                if (replace)
                {
                    await driver.SendKeysAsync(window, "ctrl+a", verbatim: false, token).ConfigureAwait(false);
                }

                await driver.SendKeysAsync(window, text, verbatim: true, token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        await SettleAsync(cancellationToken).ConfigureAwait(false);
    }
}
