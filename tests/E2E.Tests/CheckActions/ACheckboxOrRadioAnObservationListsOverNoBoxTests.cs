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
    // Partial: upstream also expects the observation to list only `Agree` and `Small` (the walk lists none a tap cannot reach), and to leave
    // out the checkbox a `display: contents` label holds and the one whose label holds a link. A box with no size is not hidden here
    // (COMPATIBILITY.md), so the port lists those too; this test checks that a tap on each node it lists over no box checks it, and that
    // a tap on the label text of a `display: contents` label sets its checkbox.
    [Fact]
    public Task Partial_Takes_a_tap_on_its_listed_node_so_the_walk_lists_none_a_tap_cannot_reach() => RunAsync(
        $"""
        <style>{ElementPlusCss}</style>
        <label class="el-checkbox"><span class="el-checkbox__input"><input class="el-checkbox__original" type="checkbox" id="agree"><span class="el-checkbox__inner"></span></span><span class="el-checkbox__label">Agree</span></label>
        <label class="el-checkbox"><span class="el-checkbox__input"><input class="el-checkbox__original" type="radio" id="small"><span class="el-checkbox__inner"></span></span><span class="el-checkbox__label">Small</span></label>
        <label style="display:contents"><input class="el-checkbox__original" type="checkbox" id="contents"><span class="el-checkbox__inner"></span>Contents</label>
        """,
        async session =>
        {
            foreach (var (role, name, id) in new[] { ("checkbox", "Agree", "agree"), ("radio", "Small", "small") })
            {
                var node = await FindAsync(session, role, name);
                Assert.Equal(0, await EvaluateAsync<double>(session, $"() => document.getElementById('{id}').getBoundingClientRect().width"));
                await session.PerformAsync(node, new LocatorAction.Tap(), CancellationToken.None);
                Assert.True(await EvaluateAsync<bool>(session, $"() => document.getElementById('{id}').checked"), name);
            }

            // The one a `display: contents` label holds is set by a tap on the label text.
            var observation = await session.ObserveAsync(CancellationToken.None);
            var text = Flatten(observation.Roots).First(node => node.Text == "Contents");
            await session.PerformAsync(text, new LocatorAction.Tap(), CancellationToken.None);
            Assert.True(await EvaluateAsync<bool>(session, "() => document.getElementById('contents').checked"));
        });
}
