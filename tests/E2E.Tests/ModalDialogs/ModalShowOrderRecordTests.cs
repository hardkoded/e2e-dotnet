// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.ModalDialogs;

/// <summary>
/// The modal show-order record, run as page code against a scripted dialog class: each opening of a dialog
/// <c>showModal()</c> has shown gets the next number, a <c>show-modal</c> command reopening one included; a call on a
/// dialog already open records nothing; a browser that fires no <c>beforetoggle</c> on a dialog is numbered by the
/// call; and a browser without <c>showModal</c> gets no record.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class ModalShowOrderRecordTests(ChromiumPage chromium) : IClassFixture<ChromiumPage>
{
    // The fakes and install helper of upstream's modal-dialogs.test.ts, as one page function: the body runs with them in scope.
    private const string Harness = """
        ([body, script]) => {
          const KEY = Symbol.for("e2e.modalDialogOrder");
          class FakeEventTarget {
            listeners = [];
            addEventListener(type, listener) { if (type === "beforetoggle") this.listeners.push(listener); }
          }
          class FakeDialog extends FakeEventTarget {
            open = false; modal = false; firesToggle = true;
            show() { this.open = true; }
            showModal() {
              if (this.open && this.modal) return;
              if (this.open) throw new Error("InvalidStateError: the dialog is already open");
              this.openModal();
            }
            openByCommand() { if (!this.open) this.openModal(); }
            close() { this.open = false; this.modal = false; }
            openModal() {
              if (this.firesToggle) for (const listener of this.listeners) listener({ newState: "open" });
              this.open = true; this.modal = true;
            }
          }
          const install = (global, dialogClass) => new Function("globalThis", "HTMLDialogElement", "EventTarget", script)(global, dialogClass, FakeEventTarget);
          const recorded = () => {
            const global = {};
            const Dialog = class extends FakeDialog {};
            install(global, Dialog);
            return { order: global[KEY], Dialog, global };
          };
          return (0, eval)(`(${body})`)({ KEY, FakeDialog, install, recorded });
        }
        """;

    private async Task<T> RunAsync<T>(string body) =>
        await chromium.Page.EvaluateAsync<T>(Harness, new object[] { body, PageScript.ModalOrderInitScript });

    [Fact]
    public async Task Numbers_every_opening_and_a_dialog_closed_and_shown_again_gets_a_new_number()
    {
        var numbers = await RunAsync<int[]>(
            """
            ({ recorded }) => {
              const { order, Dialog } = recorded();
              if (!(order instanceof WeakMap)) return [];
              const first = new Dialog();
              const second = new Dialog();
              first.showModal();
              second.showModal();
              const before = [order.get(first), order.get(second)];
              first.close();
              first.showModal();
              return [...before, order.get(first)];
            }
            """
        );
        Assert.Equal([1, 2, 3], numbers);
    }

    [Fact]
    public async Task Numbers_a_dialog_a_show_modal_command_reopens_so_it_outranks_one_shown_while_it_was_closed()
    {
        var result = await RunAsync<int?[]>(
            """
            ({ recorded }) => {
              const { order, Dialog } = recorded();
              const reopened = new Dialog();
              const between = new Dialog();
              reopened.showModal();
              reopened.close();
              between.showModal();
              reopened.openByCommand();
              // A dialog no showModal() call has seen stays unknown to the reader.
              const commandOnly = new Dialog();
              commandOnly.openByCommand();
              return [order.get(reopened), order.get(between), order.get(commandOnly) ?? null];
            }
            """
        );
        Assert.Equal([3, 2, null], result);
    }

    [Fact]
    public async Task Records_nothing_for_a_call_on_a_dialog_already_open_and_installs_once_per_document()
    {
        var result = await RunAsync<string>(
            """
            ({ recorded, install, KEY }) => {
              const { order, Dialog, global } = recorded();
              const lower = new Dialog();
              const upper = new Dialog();
              lower.showModal();
              upper.showModal();
              // The browser leaves `upper` on top, so the record must too.
              lower.showModal();
              const seen = [order.get(lower), order.get(upper)];
              const plain = new Dialog();
              plain.show();
              let threw = "";
              try { plain.showModal(); } catch (error) { threw = error.message; }
              const wrapped = Dialog.prototype.showModal;
              install(global, Dialog);
              return JSON.stringify({
                seen,
                threw: threw.includes("already open"),
                plain: order.get(plain) ?? null,
                same: global[KEY] === order && Dialog.prototype.showModal === wrapped,
              });
            }
            """
        );
        Assert.Equal("""{"seen":[1,2],"threw":true,"plain":null,"same":true}""", result);
    }

    [Fact]
    public async Task Numbers_each_showModal_call_itself_on_a_browser_that_fires_no_beforetoggle_on_a_dialog()
    {
        var numbers = await RunAsync<int[]>(
            """
            ({ recorded }) => {
              const { order, Dialog } = recorded();
              const first = new Dialog();
              const second = new Dialog();
              first.firesToggle = false;
              second.firesToggle = false;
              first.showModal();
              second.showModal();
              first.showModal();
              return [order.get(first), order.get(second)];
            }
            """
        );
        Assert.Equal([1, 2], numbers);
    }

    [Fact]
    public async Task Leaves_a_browser_without_showModal_alone()
    {
        var recorded = await RunAsync<bool>(
            """
            ({ install, KEY }) => {
              const global = {};
              install(global, undefined);
              return KEY in global;
            }
            """
        );
        Assert.False(recorded);
    }
}
