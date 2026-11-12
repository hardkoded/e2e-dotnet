// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.ShadowDomForm;

/// <summary>Ported from <c>describe('shadow DOM form')</c> in upstream's <c>apps/web-benchmark/tests/shadow-dom-form.e2e.ts</c>.</summary>
public sealed class ShadowDomFormTests : BenchmarkTest
{
    private static readonly Regex AccessCodeHint = new("Access code hint:");
    private static readonly Regex AccessCode = new(@"SHADOW-\d+");

    [SetUp]
    public async Task OpenTheShadowDomForm() => await App.OpenAsync("/e/shadow-dom-form");

    [Test]
    public async Task Rejects_a_wrong_access_code()
    {
        await Screen.GetByPlaceholder("Access code").FillAsync("SHADOW-41");
        await Screen.GetByRole("button", "Submit").TapAsync();
        await Expect.That(Screen.GetByTestId("error-message")).ToHaveTextAsync("Wrong access code");
    }

    [Test]
    public async Task Grants_access_with_the_code_from_the_hint_inside_the_root()
    {
        var code = await Support.ReadCodeAsync(Screen.GetByText(AccessCodeHint), AccessCode);
        await Screen.GetByPlaceholder("Access code").FillAsync(code);
        await Expect.That(Screen.GetByPlaceholder("Access code")).ToHaveValueAsync(code);
        await Screen.GetByRole("button", "Submit").TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Access granted");
    }
}
