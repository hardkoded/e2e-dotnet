// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.LocatorEngine;

/// <summary>Upstream's <c>describe('locator.blur')</c> (<c>packages/e2e/tests/unit/locator-engine.test.ts</c>), added by tester-army/e2e#1919.</summary>
public sealed class LocatorBlurTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Honors_the_declared_capability(bool supported)
    {
        var node = new SemanticNode
        {
            Ref = "node-1",
            Role = "button",
            Name = "Submit",
        };
        var locates = 0;
        var performed = new List<LocatorAction>();
        var screen = new Screen(
            new global::E2E.LocatorEngine(
                new global::E2E.LocatorEngineOptions
                {
                    Observe = _ => Task.FromResult(new Observation { Route = "/", Roots = [node] }),
                    Perform = (_, action, _, _) =>
                    {
                        performed.Add(action);
                        return Task.CompletedTask;
                    },
                    Cancellation = () => CancellationToken.None,
                    ActionTimeout = TimeSpan.FromSeconds(1),
                    AssertionTimeout = TimeSpan.FromSeconds(1),
                    Locate = (_, _, _) =>
                    {
                        locates++;
                        return Task.FromResult<IReadOnlyList<SemanticNode>>([node]);
                    },
                }
            ),
            () => { }
        )
        {
            ActionKinds = supported ? ["blur"] : ["tap"],
        };

        Task Blur() => screen.GetByRole("button", new RoleOptions { Name = "Submit" }).BlurAsync();

        if (supported)
        {
            await Blur();
            Assert.IsType<LocatorAction.Blur>(Assert.Single(performed));
        }
        else
        {
            var error = await Assert.ThrowsAsync<EngineException>(Blur);
            Assert.Equal(EngineErrorCodes.UnsupportedCapability, error.Code);
            Assert.Equal(0, locates);
            Assert.Empty(performed);
        }
    }
}
