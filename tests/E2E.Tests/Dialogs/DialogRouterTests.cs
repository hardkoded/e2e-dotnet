// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.Dialogs;

/// <summary>Upstream's <c>describe('DialogRouter')</c> (<c>packages/web/tests/unit/dialogs.test.ts</c>).</summary>
public sealed class DialogRouterTests
{
    [Fact]
    public async Task Routes_to_the_newest_handler_and_falls_back_once_it_unsubscribes()
    {
        var router = new DialogRouter();
        var first = 0;
        var second = 0;
        router.Add((DialogHandler)(_ =>
        {
            first++;
            return Task.CompletedTask;
        }));
        var unsubscribe = router.Add((DialogHandler)(_ =>
        {
            second++;
            return Task.CompletedTask;
        }));

        await router.DispatchAsync(new FakeNativeDialog());
        Assert.Equal(1, second);
        Assert.Equal(0, first);

        unsubscribe();
        await router.DispatchAsync(new FakeNativeDialog());
        Assert.Equal(1, first);
    }

    [Fact]
    public async Task Tracks_registrations_by_identity_the_same_handler_twice_unsubscribed_once_stays()
    {
        var router = new DialogRouter();
        var calls = 0;
        var handler = (DialogHandler)(dialog =>
        {
            calls++;
            return dialog.AcceptAsync();
        });
        var unsubscribeFirst = router.Add(handler);
        router.Add(handler);
        unsubscribeFirst();
        unsubscribeFirst();

        await router.DispatchAsync(new FakeNativeDialog());
        Assert.Equal(1, calls);
        router.ThrowPending();
    }

    [Fact]
    public async Task Applies_the_accept_and_dismiss_shorthands_and_passes_the_message_through()
    {
        var router = new DialogRouter();
        router.Add(DialogHandler.Accept);
        var accepted = new FakeNativeDialog();
        await router.DispatchAsync(accepted);
        Assert.Equal(1, accepted.Accepts);

        router.Add(DialogHandler.Dismiss);
        var dismissed = new FakeNativeDialog();
        await router.DispatchAsync(dismissed);
        Assert.Equal(1, dismissed.Dismisses);

        var seen = new List<string>();
        router.Add((DialogHandler)(dialog =>
        {
            seen.Add(dialog.Message);
            return dialog.AcceptAsync("yes");
        }));
        var custom = new FakeNativeDialog("name?");
        await router.DispatchAsync(custom);
        Assert.Equal(["name?"], seen);
        Assert.Equal(1, custom.Accepts);
        Assert.Equal("yes", custom.AcceptedText);
    }

    [Fact]
    public async Task Dismisses_a_dialog_its_handler_left_undecided_and_latches_INVALID_STATE_naming_the_handler()
    {
        var router = new DialogRouter();

        // Looked, decided nothing: the page would stay blocked behind the dialog.
        router.Add((DialogHandler)(_ => Task.CompletedTask));
        var dialog = new FakeNativeDialog("are you sure?");
        await router.DispatchAsync(dialog);
        Assert.Equal(1, dialog.Dismisses);
        var error = Assert.Throws<EngineException>(router.ThrowPending);
        Assert.Equal("INVALID_STATE", error.Code);
        Assert.Contains("returned without calling accept or dismiss", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dismisses_an_unhandled_dialog_and_latches_INVALID_STATE_thrown_exactly_once()
    {
        var router = new DialogRouter();
        var dialog = new FakeNativeDialog("unexpected");
        await router.DispatchAsync(dialog);
        Assert.Equal(1, dialog.Dismisses);
        var error = Assert.Throws<EngineException>(router.ThrowPending);
        Assert.Equal("INVALID_STATE", error.Code);
        Assert.Contains("unexpected", error.Message, StringComparison.Ordinal);
        router.ThrowPending();
    }

    [Fact]
    public async Task Latches_a_failing_handler_as_ENGINE_FAILURE_thrown_once()
    {
        var router = new DialogRouter();
        router.Add((DialogHandler)(_ => throw new InvalidOperationException("handler exploded")));
        var dialog = new FakeNativeDialog();
        await router.DispatchAsync(dialog);
        Assert.Equal(1, dialog.Dismisses);
        var error = Assert.Throws<EngineException>(router.ThrowPending);
        Assert.Equal("ENGINE_FAILURE", error.Code);
        Assert.Contains("handler exploded", error.Message, StringComparison.Ordinal);

        router.ThrowPending();
    }

    [Fact]
    public async Task Dismisses_a_dialog_when_accepting_rejects_and_leaves_it_unanswered()
    {
        var router = new DialogRouter();
        var dialog = new FakeNativeDialog { AcceptFailure = new InvalidOperationException("accept failed") };
        router.Add((DialogHandler)(native => native.AcceptAsync()));
        await router.DispatchAsync(dialog);
        Assert.Equal(1, dialog.Dismisses);
        Assert.Equal("ENGINE_FAILURE", Assert.Throws<EngineException>(router.ThrowPending).Code);
    }

    [Fact]
    public async Task Accepts_a_void_handler_that_starts_an_answer_without_awaiting_it()
    {
        var router = new DialogRouter();
        var dialog = new FakeNativeDialog();
        router.Add((DialogHandler)(native =>
        {
            _ = native.AcceptAsync();
            return Task.CompletedTask;
        }));
        await router.DispatchAsync(dialog);
        Assert.Equal(1, dialog.Accepts);
        Assert.Equal(0, dialog.Dismisses);
        router.ThrowPending();
    }

    [Fact]
    public async Task Latches_a_classified_handler_failure_as_it_was_an_assertion_stays_ASSERTION_FAILED()
    {
        var router = new DialogRouter();
        var failure = new TestException("ASSERTION_FAILED", "expected the other message");
        router.Add((DialogHandler)(async dialog =>
        {
            await dialog.AcceptAsync();
            throw failure;
        }));
        await router.DispatchAsync(new FakeNativeDialog());
        Assert.Same(failure, Assert.Throws<TestException>(router.ThrowPending));
    }

    [Fact]
    public async Task Shares_one_latch_with_its_owner_so_either_side_observes_the_failure()
    {
        var latch = new ErrorLatch();
        var router = new DialogRouter(latch);
        await router.DispatchAsync(new FakeNativeDialog());
        Assert.Equal("INVALID_STATE", Assert.Throws<EngineException>(latch.ThrowPending).Code);
        router.ThrowPending();
    }
}
