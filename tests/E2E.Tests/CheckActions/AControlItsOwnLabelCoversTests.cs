// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.CheckActions.CheckPage;
using static E2E.Tests.CheckActions.CoveredControl;

namespace E2E.Tests.CheckActions;

/// <summary>
/// A control its own label covers, ported from upstream <c>check-actions.test.ts</c> <c>describe.each(KINDS)('a control its own label covers,
/// through the %s target')</c>. .NET-only: the two kinds are one base class, run once through a screen locator and once through the element an
/// observation names.
/// </summary>
[Collection(BrowserCollection.Name)]
public abstract class AControlItsOwnLabelCoversTests(bool locator)
{
    public static IEnumerable<object[]> Cases() => ElementPlusCases.Select(each => new object[] { each.Name, each.Id });

    protected Task RunAsync(string body, Func<CoveredControl, Task> test, TimeSpan? timeout = null) => CoveredControl.RunAsync(locator, body, test, timeout);

    [Theory]
    [MemberData(nameof(Cases))]
    public Task Checks_an_Element_Plus_control(string name, string id) => RunAsync(ElementPlusPage, async control =>
    {
        Assert.Null(await control.PerformAsync(id, "check"));
        Assert.True(await control.IsCheckedAsync(id), name);
    });

    [Theory]
    [MemberData(nameof(Cases))]
    public Task Taps_an_Element_Plus_control(string name, string id) => RunAsync(ElementPlusPage, async control =>
    {
        Assert.Null(await control.PerformAsync(id, "tap"));
        Assert.True(await control.IsCheckedAsync(id), name);
    });

    [Fact]
    public Task Unchecks_an_Element_Plus_checkbox_and_refuses_an_Element_Plus_radio_without_clicking_it() => RunAsync(CountClicks + ElementPlusPage, async control =>
    {
        await control.EvaluateAsync("() => { for (const id of ['agree', 'large']) document.getElementById(id).checked = true; return ''; }");
        Assert.Null(await control.PerformAsync("agree", "uncheck"));
        Assert.False(await control.IsCheckedAsync("agree"));
        var before = await control.EvaluateAsync("() => String(window.clicks)");
        Assert.Equal("NOT_ACTIONABLE", (await control.PerformAsync("large", "uncheck"))?.Code);
        Assert.True(await control.IsCheckedAsync("large"));
        Assert.Equal(before, await control.EvaluateAsync("() => String(window.clicks)"));
    });

    [Theory]
    [MemberData(nameof(Cases))]
    public Task Still_refuses_an_Element_Plus_control_under_a_modal(string name, string id) => RunAsync(ElementPlusPage + """<div class="modal"></div>""", async control =>
    {
        foreach (var verb in new[] { "check", "tap" })
        {
            Assert.Equal("NOT_ACTIONABLE", (await control.PerformAsync(id, verb))?.Code);
            Assert.False(await control.IsCheckedAsync(id), name);
        }
    }, RefusalTimeout);

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Waits_out_a_loading_mask_over_an_Element_Plus_control(string name, string id)
    {
        foreach (var verb in new[] { "check", "tap" })
        {
            await RunAsync(
                ElementPlusPage + """<div class="modal"></div><script>setTimeout(() => document.querySelector('.modal').remove(), 300)</script>""",
                async control =>
                {
                    Assert.Null(await control.PerformAsync(id, verb));
                    Assert.True(await control.IsCheckedAsync(id), name);
                });
        }
    }

    [Fact]
    public Task Still_refuses_a_radio_whose_label_covers_it_from_outside_the_label_box() => RunAsync(
        """
        <label style="display:inline-block;width:60px;height:20px">Pick<input type="radio" id="r" data-testid="r"
          style="position:absolute;left:200px;top:100px;opacity:0;z-index:-1"><span
          style="position:absolute;left:190px;top:90px;width:40px;height:40px;background:#fff"></span></label>
        """,
        async control =>
        {
            foreach (var verb in new[] { "check", "tap" })
            {
                var failure = await control.PerformAsync("r", verb);
                Assert.Equal("NOT_ACTIONABLE", failure?.Code);
                Assert.Contains("intercepts pointer events", failure?.Message);
                Assert.False(await control.IsCheckedAsync("r"));
            }
        },
        RefusalTimeout);

    [Fact]
    public Task Still_refuses_a_radio_under_a_link_in_its_own_label_and_follows_no_link() => RunAsync(
        $"""
        <style>{ElementPlusCss}</style>
        <label class="el-radio-button"><input class="el-radio-button__original-radio" type="radio" name="mode" id="install" data-testid="install">
          <a href="#went" class="el-radio-button__inner">安装</a></label>
        """,
        async control =>
        {
            foreach (var verb in new[] { "check", "tap" })
            {
                var failure = await control.PerformAsync("install", verb);
                Assert.Equal("NOT_ACTIONABLE", failure?.Code);
                Assert.Contains("intercepts pointer events", failure?.Message);
                Assert.False(await control.IsCheckedAsync("install"));
                Assert.DoesNotContain("#went", await control.EvaluateAsync("() => location.href"));
            }
        },
        RefusalTimeout);

    [Fact]
    public Task Still_refuses_a_disabled_Element_Plus_radio_button() => RunAsync(
        ElementPlusPage.Replace("id=\"install\"", "id=\"install\" disabled", StringComparison.Ordinal),
        async control =>
        {
            foreach (var verb in new[] { "check", "tap" })
            {
                Assert.Equal("NOT_ACTIONABLE", (await control.PerformAsync("install", verb))?.Code);
                Assert.False(await control.IsCheckedAsync("install"));
            }
        },
        RefusalTimeout);

    [Fact]
    public Task Checks_an_Element_Plus_radio_button_inside_a_shadow_root() => RunAsync(
        $$"""
        <div id="host"></div><script>
          document.getElementById('host').attachShadow({ mode: 'open' }).innerHTML = {{System.Text.Json.JsonSerializer.Serialize(ElementPlusPage)}};
        </script>
        """,
        async control =>
        {
            Assert.Null(await control.PerformAsync("install", "check"));
            Assert.True(await control.IsCheckedAsync("install"));
        });

}

public sealed class AControlItsOwnLabelCoversThroughTheLocatorTargetTests() : AControlItsOwnLabelCoversTests(locator: true)
{
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Waits_for_an_Element_Plus_control_in_a_dialog_that_opens_later(string name, string id)
    {
        foreach (var display in new[] { "display:none", "visibility:hidden" })
        {
            foreach (var verb in new[] { "check", "tap" })
            {
                await RunAsync(
                    $"""
                    <style>{ElementPlusCss}</style><div id="dialog" style="{display}">{ElementPlusControls}</div>
                    <script>setTimeout(() => document.getElementById('dialog').removeAttribute('style'), 300)</script>
                    """,
                    async control =>
                    {
                        Assert.Null(await control.PerformAsync(id, verb));
                        Assert.True(await control.IsCheckedAsync(id), name);
                    });
            }
        }
    }
}

public sealed class AControlItsOwnLabelCoversThroughTheElementTargetTests() : AControlItsOwnLabelCoversTests(locator: false)
{
    [Theory(Skip = "Upstream reaches a checkbox with no box that no label draws through the element itself; its walk leaves it out, so an observation names no node to act on here.")]
    [MemberData(nameof(Cases))]
    public Task Waits_for_an_Element_Plus_control_in_a_dialog_that_opens_later(string name, string id) => Task.FromResult((name, id));

    [Fact]
    public Task Checks_a_boxless_checkbox_through_its_first_visible_label_past_a_hidden_one() => RunAsync(
        """
        <input type="checkbox" id="c" data-testid="c" style="display:none">
        <label for="c" style="display:none">A</label>
        <label for="c">B</label>
        """,
        async control =>
        {
            Assert.Null(await control.PerformAsync("c", "check"));
            Assert.True(await control.IsCheckedAsync("c"));
        });

    [Fact]
    public Task Checks_a_boxless_checkbox_past_a_label_hidden_by_visibility_which_keeps_its_box() => RunAsync(
        """
        <input type="checkbox" id="c" data-testid="c" style="display:none">
        <label for="c" style="visibility:hidden">A</label>
        <label for="c">B</label>
        """,
        async control =>
        {
            Assert.Null(await control.PerformAsync("c", "check"));
            Assert.True(await control.IsCheckedAsync("c"));
        });

    [Fact(Skip = "Upstream reaches a checkbox with no box that no label draws through the element itself; its walk leaves it out, so an observation names no node to act on here.")]
    public Task Still_refuses_a_boxless_checkbox_whose_visible_label_has_a_link_at_its_centre() => RunAsync(
        """
        <input type="checkbox" id="c" data-testid="c" style="display:none">
        <label for="c" style="display:inline-block"><a href="#went">Terms</a></label>
        """,
        async control =>
        {
            foreach (var verb in new[] { "check", "tap" })
            {
                var failure = await control.PerformAsync("c", verb);
                Assert.Equal("NOT_ACTIONABLE", failure?.Code);
                Assert.Contains("not visible", failure?.Message);
                Assert.False(await control.IsCheckedAsync("c"));
                Assert.DoesNotContain("#went", await control.EvaluateAsync("() => location.href"));
            }
        },
        RefusalTimeout);

    [Fact]
    public Task Still_refuses_an_Element_Plus_radio_button_a_disabled_fieldset_holds() => RunAsync(
        $"<style>{ElementPlusCss}</style><fieldset disabled>{ElementPlusControls}</fieldset>",
        async control =>
        {
            foreach (var verb in new[] { "check", "tap" })
            {
                var failure = await control.PerformAsync("install", verb);
                Assert.Equal("NOT_ACTIONABLE", failure?.Code);
                Assert.Contains("not enabled", failure?.Message);
                Assert.False(await control.IsCheckedAsync("install"));
            }
        },
        RefusalTimeout);

    [Fact(Skip = "Upstream reaches a checkbox with no box that no label draws through the element itself; its walk leaves it out, so an observation names no node to act on here.")]
    public Task Still_refuses_a_boxless_checkbox_whose_every_label_is_hidden() => RunAsync(
        """
        <input type="checkbox" id="c" data-testid="c" style="display:none">
        <label for="c" style="display:none">A</label>
        """,
        async control =>
        {
            foreach (var verb in new[] { "check", "tap" })
            {
                var failure = await control.PerformAsync("c", verb);
                Assert.Equal("NOT_ACTIONABLE", failure?.Code);
                Assert.Equal($"{verb} did not become actionable in time: the control and its labels are not visible", failure?.Message);
                Assert.False(await control.IsCheckedAsync("c"));
            }
        },
        RefusalTimeout);

    [Fact]
    public Task Clicks_nothing_for_an_action_cancelled_while_its_stand_in_read_is_in_flight() => RunAsync(
        ElementPlusPage + """<input type="checkbox" id="plain" data-testid="plain">""",
        async control =>
        {
            foreach (var (id, verb) in new[] { ("install", "tap"), ("install", "check"), ("plain", "tap") })
            {
                using var cancelled = new CancellationTokenSource();
                // Holds the page's main thread once the control is resolved, so the read waits behind it until after the abort.
                async Task Hold()
                {
                    await control.EvaluateAsync("() => { setTimeout(() => { const until = Date.now() + 400; while (Date.now() < until); }, 0); return ''; }");

                    // Lets the page start the hold before the action's first read is sent.
                    await Task.Delay(50);
                    cancelled.CancelAfter(TimeSpan.FromMilliseconds(100));
                }

                Assert.Equal("CANCELLED", (await control.PerformAsync(id, verb, cancelled.Token, Hold))?.Code);
                Assert.False(await control.IsCheckedAsync(id));
            }
        });
}

