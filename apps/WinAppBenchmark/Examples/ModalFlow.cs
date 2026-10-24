// Copyright 2026 Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.UI.Xaml.Controls;
using static WinAppBenchmark.App.Support;

namespace WinAppBenchmark.Examples;

/// <summary>
/// A modal card over the screen, then a dialog over the modal. The card is an in-window overlay, as
/// React Native's transparent <c>Modal</c> is; the dialog is a <c>ContentDialog</c>, the desktop's own
/// alert, which the Windows fixture answers with <c>AlertAsync</c>.
/// </summary>
public sealed class ModalFlow : Component
{
    public override Element Render()
    {
        var (modalVisible, setModalVisible) = UseState(false);
        var (confirmOpen, setConfirmOpen) = UseState(false);
        var (confirmed, setConfirmed) = UseState(false);

        var screen = VStack(12, confirmed
            ? Success("Flow completed")
            : Button("Open modal", () => setModalVisible(true)).AutomationId("open-modal-button"))
            .Padding(24);

        var card = ScrollViewer(VStack(12,
                Subtitle("Benchmark modal"),
                Body("Tap Continue, then accept the dialog to finish.").TextWrapping(),
                Button("Continue", () => setConfirmOpen(true)).AutomationId("continue-button"),
                Button("Close", () => setModalVisible(false)).AutomationId("close-modal-button"))
                .Padding(20))
            .AutomationId("modal-card")
            .Background(Theme.CardBackground)
            .CornerRadius(16, 16, 0, 0)
            .VAlign(VerticalAlignment.Bottom)
            .HAlign(HorizontalAlignment.Stretch);

        return Grid(
            columns: [GridSize.Star()],
            rows: [GridSize.Star()],
            screen,
            When(modalVisible, () => Border(card).Background("#80000000")),
            ContentDialog("Confirm action", Body("Do you want to complete this flow?"), "Confirm") with
            {
                IsOpen = confirmOpen,
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                OnClosed = result =>
                {
                    setConfirmOpen(false);
                    if (result == ContentDialogResult.Primary)
                    {
                        setModalVisible(false);
                        setConfirmed(true);
                    }
                },
            });
    }
}
