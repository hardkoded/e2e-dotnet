// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.CheckActions.CheckPage;
using static E2E.Tests.CheckActions.CoveredControl;

namespace E2E.Tests.CheckActions;

/// <summary>A checkbox or radio an observation lists over no box. Ported from upstream <c>check-actions.test.ts</c>.</summary>
[Collection(BrowserCollection.Name)]
public sealed class ACheckboxOrRadioAnObservationListsOverNoBoxTests
{
    [Fact]
    public Task Takes_a_tap_on_its_listed_node_so_the_walk_lists_none_a_tap_cannot_reach() => RunAsync(
        $"""
        <style>{ElementPlusCss}</style>
        <label class="el-checkbox"><span class="el-checkbox__input"><input class="el-checkbox__original" type="checkbox" id="agree"><span class="el-checkbox__inner"></span></span><span class="el-checkbox__label">Agree</span></label>
        <label class="el-checkbox"><span class="el-checkbox__input"><input class="el-checkbox__original" type="radio" id="small"><span class="el-checkbox__inner"></span></span><span class="el-checkbox__label">Small</span></label>
        <label style="display:contents"><input class="el-checkbox__original" type="checkbox" id="contents"><span class="el-checkbox__inner"></span>Contents</label>
        <label class="el-checkbox"><span class="el-checkbox__input"><input class="el-checkbox__original" type="checkbox" id="terms"><span class="el-checkbox__inner"></span></span><span class="el-checkbox__label">Accept <a href="#went">the Terms and Conditions</a></span></label>
        """,
        async session =>
        {
            var observed = await session.ObserveAsync(CancellationToken.None);
            var listed = Flatten(observed.Roots).Where(node => node.Role is "checkbox" or "radio").ToList();
            Assert.Equal(["Agree", "Small"], listed.Select(node => node.Name));
            foreach (var node in listed)
            {
                var id = node.Name == "Agree" ? "agree" : "small";
                Assert.Equal(0, await EvaluateAsync<double>(session, $"() => document.getElementById('{id}').getBoundingClientRect().width"));
                await session.PerformAsync(node, new LocatorAction.Tap(), CancellationToken.None);
                Assert.True(await EvaluateAsync<bool>(session, $"() => document.getElementById('{id}').checked"), node.Name);
            }

            // The one a `display: contents` label holds is left out; a tap on the label text sets it.
            var text = Flatten(observed.Roots).First(node => node.Text == "Contents");
            await session.PerformAsync(text, new LocatorAction.Tap(), CancellationToken.None);
            Assert.True(await EvaluateAsync<bool>(session, "() => document.getElementById('contents').checked"));
        });
}
