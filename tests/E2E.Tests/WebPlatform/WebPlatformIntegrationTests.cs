// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;
using E2E.Reports;

namespace E2E.Tests.WebPlatform;

[Collection(BrowserCollection.Name)]
public sealed class WebPlatformIntegrationTests
{
    // Partial: upstream "passes" also checks the clicks that hold modifiers, and report.json. Not ported, the port has no such feature: click
    // modifiers and secondaryTap on a locator, and report.json. The upstream tests that read the harness's step records are ported
    // below over the session's steps.
    [Theory]
    [InlineData("deterministic queries and reads")]
    [InlineData("a textarea value compares raw")]
    [InlineData("toHaveAttribute reads the value attribute of plain fields")]
    [InlineData("a viewport set before the first navigation holds through app.open")]
    [InlineData("reads wait for a node that arrives late")]
    [InlineData("class assertions report the observed class on failure")]
    [InlineData("class assertions poll for a node that arrives late")]
    [InlineData("an empty class attribute is an empty class list")]
    [InlineData("reads under an absent frame answer with no match")]
    [InlineData("a nested absent frame counts as zero matches at once")]
    [InlineData("absence matchers and waits under an absent frame pass at once")]
    [InlineData("a *css= capture is refused before the last selector part")]
    [InlineData("toHaveURL ignoreCase folds the comparison")]
    [InlineData("string and RegExp URL patterns match what they name")]
    [InlineData("routes are attempt-scoped: registered before the first page, kept across restart and clearState")]
    [InlineData("contenteditable hosts are textboxes: reached by label and by role (a bare host too), filled, and read as a value")]
    public async Task Ported_scenario_passes(string title)
    {
        Assert.Null(await RunAsync(Scenarios[title]));
    }

    [Fact]
    public async Task Downloads_are_captured_as_artifacts()
    {
        Assert.Null(await RunAsync(async session =>
        {
            await session.App.OpenAsync("/downloads");
            var download = await session.Browser.WaitForDownloadAsync(() => session.Browser.Locator("a[download]").TapAsync());
            Assert.Equal("report.csv", download.SuggestedFilename);
            Assert.StartsWith("downloads/", download.Path, StringComparison.Ordinal);
            Assert.Equal("id,total\n1,42\n", await File.ReadAllTextAsync(download.AbsolutePath));
        }));
    }

    [Fact]
    public async Task Fails_a_download_that_never_started_with_the_wait_and_the_trigger_time_not_a_bare_timeout()
    {
        var error = Assert.IsType<TestException>(await RunAsync(async session =>
        {
            await session.App.OpenAsync("/downloads");
            await session.Browser.WaitForDownloadAsync(() => Task.Delay(200), TimeSpan.FromMilliseconds(500));
        }));
        Assert.Equal("ACTION_FAILED", error.Code);
        var timing = Regex.Match(error.Message, @"^no download started within (\d+)ms; the trigger resolved after (\d+)ms$");
        Assert.True(timing.Success, error.Message);

        // The operation reports its remaining budget after setup, capped by the requested 500ms.
        var waitMs = int.Parse(timing.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(waitMs, 1, 500);

        // The trigger sleeps 200ms; a timer can fire a millisecond or two early.
        Assert.True(int.Parse(timing.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture) >= 190, error.Message);
    }

    [Fact]
    public async Task Attributes_a_download_to_the_fixture_step_that_produced_it_not_the_nested_tap()
    {
        var results = Directory.CreateTempSubdirectory("e2e-download-report-").FullName;
        try
        {
            using var site = await FixtureApp.StartAsync();
            await using var session = await E2ESession.StartAsync(new E2ESessionOptions
            {
                Engine = new WebEngine(headless: true),
                Model = new ScriptedModel(_ => ModelResponses.Done("passed", "unused")),
                BaseUrl = site.Url,
                TestTitle = "downloads are captured as artifacts",
                ProjectRoot = results,
                ResultsDirectory = Path.Combine(results, "results"),
                ActionTimeout = TimeSpan.FromSeconds(5),
                TestTimeout = TimeSpan.FromSeconds(30),
                CacheEnabled = false,
                Trace = new ResolvedRecording(RecordingMode.RetainOnFailure, RecordingSource.Target),
            });
            await session.App.OpenAsync("/downloads");
            await session.Browser.WaitForDownloadAsync(() => session.Browser.Locator("a[download]").TapAsync());
            session.Complete();
            await session.DisposeAsync();

            var attempt = session.Report()!.Run.Results[0].Attempts[0];
            var wait = attempt.Steps.First(step => step.Api == "browser.waitForDownload");
            Assert.Single(wait.Artifacts);
            Assert.Empty(attempt.Steps.First(step => step.Api == "locator.tap").Artifacts);
            var download = attempt.Artifacts.First(artifact => artifact.Kind == ArtifactKind.Download);
            Assert.Contains("downloads/", download.Path, StringComparison.Ordinal);

            // Bytes the app served, not rewritten by the runner: the record says so.
            Assert.Equal("text/csv", download.MediaType);
            Assert.Equal(ArtifactRedaction.Incomplete, download.Redaction);
        }
        finally
        {
            Directory.Delete(results, recursive: true);
        }
    }

    [Fact]
    public async Task Fails_a_read_of_a_node_that_never_arrives_with_LOCATOR_NOT_FOUND_once_the_action_timeout_has_run()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var error = Assert.IsType<TestException>(await RunAsync(async session =>
        {
            await session.App.OpenAsync("/classes");
            await session.Screen.GetByText("Never card").TextContentAsync();
        }));
        Assert.Equal("LOCATOR_NOT_FOUND", error.Code);
        Assert.Contains("Never card", error.Message, StringComparison.Ordinal);
        Assert.True(watch.Elapsed >= TimeSpan.FromSeconds(5), "failed after " + watch.Elapsed);
    }

    [Fact]
    public async Task Decides_absence_under_an_absent_frame_without_waiting_out_the_deadline()
    {
        var (error, steps) = await RunWithStepsAsync(Scenarios["absence matchers and waits under an absent frame pass at once"]);
        Assert.Null(error);
        var decided = steps.Where(step => step.Api.StartsWith("expect.", StringComparison.Ordinal) || step.Api == "locator.waitFor").ToList();
        Assert.Equal(5, decided.Count);
        foreach (var step in decided)
        {
            Assert.True(step.DurationMs < 3000, step.Api + " took " + step.DurationMs + "ms");
        }
    }

    [Fact]
    public async Task Fails_an_action_under_an_absent_frame_with_LOCATOR_NOT_FOUND_once_its_timeout_has_run()
    {
        var (error, steps) = await RunWithStepsAsync(async session =>
        {
            await session.App.OpenAsync("/frame");
            await session.Browser.FrameLocator("#absent").GetByRole("button").TapAsync(new ActionOptions { Timeout = TimeSpan.FromMilliseconds(800) });
        });
        Assert.Equal("LOCATOR_NOT_FOUND", Assert.IsType<TestException>(error).Code);
        var tap = steps.First(step => step.Api == "locator.tap");
        Assert.Equal(StepStatus.Failed, tap.Status);
        Assert.True(tap.DurationMs >= 800, "the tap failed after " + tap.DurationMs + "ms");
    }

    [Fact]
    public async Task Keeps_a_class_assertion_polling_while_the_node_is_missing_and_passes_once_it_arrives()
    {
        var (error, steps) = await RunWithStepsAsync(Scenarios["class assertions poll for a node that arrives late"]);
        Assert.Null(error);
        Assert.True(steps.First(step => step.Api == "expect.toHaveClass").DurationMs > 500);
    }

    [Fact]
    public async Task Fails_a_class_assertion_on_an_ambiguous_locator_with_LOCATOR_AMBIGUOUS_before_the_deadline()
    {
        var (error, steps) = await RunWithStepsAsync(async session =>
        {
            await session.App.OpenAsync("/classes");
            await Expect.That(session.Browser).ToHaveClassAsync(session.Browser.Locator(".dup"), "dup", TimeSpan.FromMilliseconds(1800));
        });
        Assert.Equal("LOCATOR_AMBIGUOUS", Assert.IsType<TestException>(error).Code);
        Assert.True(steps.First(step => step.Api == "expect.toHaveClass").DurationMs < 1800);
    }

    [Fact]
    public async Task Fails_a_class_assertion_on_a_node_that_never_arrives_as_ASSERTION_FAILED_observing_no_node()
    {
        var (error, steps) = await RunWithStepsAsync(async session =>
        {
            await session.App.OpenAsync("/classes");
            await Expect.That(session.Browser).ToHaveClassAsync(session.Browser.Locator("#never-card"), "card", TimeSpan.FromMilliseconds(300));
        });
        var failure = Assert.IsType<TestException>(error);
        Assert.Equal("ASSERTION_FAILED", failure.Code);
        Assert.Contains("observed: no node", failure.Message, StringComparison.Ordinal);
        Assert.True(steps.First(step => step.Api == "expect.toHaveClass").DurationMs < 3000);
    }

    [Fact]
    public async Task Matches_an_empty_class_string_against_no_class_attribute_only_when_one_is_present()
    {
        var error = await RunAsync(async session =>
        {
            await session.App.OpenAsync("/classes");
            await Expect.That(session.Browser).ToHaveClassAsync(session.Screen.GetByTestId("items"), "", TimeSpan.FromMilliseconds(300));
        });
        var missing = Assert.IsType<TestException>(error);
        Assert.Equal("ASSERTION_FAILED", missing.Code);
        Assert.Contains("observed: no class attribute", missing.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sets_a_viewport_before_the_first_navigation_without_opening_a_page()
    {
        var (error, steps) = await RunWithStepsAsync(Scenarios["a viewport set before the first navigation opens no page"]);
        Assert.Equal(
            [("browser.setViewport", StepStatus.Passed), ("locator.tap", StepStatus.Failed)],
            steps.Select(step => (step.Api, step.Status)).ToArray());
        Assert.Equal("APP_NOT_OPEN", Assert.IsType<TestException>(error).Code);
    }

    [Fact]
    public async Task Fails_toHaveValue_on_a_textarea_against_the_raw_value_and_prints_what_it_compared()
    {
        // The web engine reports the value raw, and the matcher neither collapses the newlines to match nor prints a string it never compared.
        var error = Assert.IsType<TestException>(await RunAsync(Scenarios["a textarea value is never normalized to match"]));
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Contains("observed value \"line1\\n\\nline2  \"", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Denies_toHaveAttribute_on_a_password_field_that_has_a_value_attribute_with_POLICY_DENIED_negated_too()
    {
        // The engine withholds a secure field's value attribute, so the matcher must not read the gap as absent and pass the negated form.
        foreach (var title in new[] { "toHaveAttribute never judges a secure field", "a negated toHaveAttribute never judges a secure field" })
        {
            var error = Assert.IsType<TestException>(await RunAsync(Scenarios[title]));
            Assert.Equal("POLICY_DENIED", error.Code);
            Assert.Contains("reading values from a secure field is denied", error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("marker-5e0c", error.ToString(), StringComparison.Ordinal);
        }

        var control = Assert.IsType<TestException>(await RunAsync(Scenarios["a negated toHaveAttribute fails on a plain field that holds the same marker"]));
        Assert.Equal("ASSERTION_FAILED", control.Code);
        Assert.Contains("observed attribute \"value\" \"marker-5e0c\"", control.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Denies_secure_value_reads_with_POLICY_DENIED()
    {
        var error = Assert.IsType<TestException>(await RunAsync(Scenarios["secure fields refuse value reads"]));
        Assert.Equal("POLICY_DENIED", error.Code);
    }

    [Fact]
    public async Task Denies_toHaveValue_on_a_filled_password_field_with_POLICY_DENIED_never_judging_it_empty()
    {
        // The engine withholds a secure field's value, so the matcher must not read the gap as '' and pass a filled password field as cleared.
        var error = Assert.IsType<TestException>(await RunAsync(Scenarios["toHaveValue never judges a secure field"]));
        Assert.Equal("POLICY_DENIED", error.Code);
        Assert.Contains("reading values from a secure field is denied", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("marker-9b1f", error.ToString(), StringComparison.Ordinal);
        var control = Assert.IsType<TestException>(await RunAsync(Scenarios["toHaveValue reads a plain field that holds the same marker"]));
        Assert.Equal("ASSERTION_FAILED", control.Code);
        Assert.Contains("observed value \"marker-9b1f\"", control.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fails_a_negated_toHaveURL_ignoreCase_on_a_case_folded_match()
    {
        var error = Assert.IsType<TestException>(await RunAsync(async session =>
        {
            await session.App.OpenAsync("/classes");
            await Expect.That(session.Browser).Not.ToHaveURLAsync("/CLASSES", ignoreCase: true, timeout: TimeSpan.FromMilliseconds(300));
        }));
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Matches("^expect\\.not\\.toHaveURL failed\nexpected: not URL /CLASSES \\(ignoring case\\)\nobserved: URL http://127\\.0\\.0\\.1:\\d+/classes$", error.Message);
    }

    [Fact]
    public async Task Denies_navigation_to_a_forbidden_scheme()
    {
        var error = Assert.IsType<TestException>(await RunAsync(Scenarios["forbidden URL schemes are refused"]));
        Assert.Equal("POLICY_DENIED", error.Code);
    }

    // Partial: upstream also reads the report (phase 'body', attempt.cleanup 'complete', no run errors). The port's session fails with the
    // handler's error, and the session releases without throwing; the phase and the cleanup field are the runner's and are not asserted.
    // The handler's assertion throws ASSERTION_FAILED by hand: the port has no expect(value).toBe.
    [Theory]
    [InlineData("route", "a route handler assertion that no step follows")]
    [InlineData("dialog", "a dialog handler assertion that no step follows")]
    public async Task Partial_Fails_a_handler_assertion_at_the_attempt_end_when_no_step_follows_it(string kind, string title)
    {
        var error = Assert.IsType<TestException>(await RunAsync(Scenarios[title]));
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Contains("Expected: \"" + (kind == "route" ? "POST" : "Are you sure?") + "\"", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Denies_a_wrapped_or_non_http_s_scheme_on_app_open_and_browser_goto_before_it_loads()
    {
        // A local file the wrapped schemes would load. A navigation that went through would show its text.
        var directory = Directory.CreateTempSubdirectory("e2e-schemes");
        var markerPath = Path.Combine(directory.FullName, "marker.txt");
        await File.WriteAllTextAsync(markerPath, "marker-local-file\n");
        var marker = new Uri(markerPath).AbsoluteUri;

        foreach (var url in new[] { "view-source:" + marker, "VIEW-SOURCE:" + marker, "  view-source:" + marker, "blob:http://127.0.0.1/x", "about:srcdoc" })
        {
            var error = Assert.IsType<TestException>(await RunAsync(async session =>
            {
                await session.App.OpenAsync();
                await session.App.OpenAsync(url);
                Assert.DoesNotContain("marker-local-file", await session.Browser.EvaluateAsync<string>("() => document.body?.innerText ?? ''"));
            }));
            Assert.Equal("POLICY_DENIED", error.Code);
            Assert.Matches("^forbidden URL scheme: (view-source|blob|about):$", error.Message);
        }

        var navigated = Assert.IsType<TestException>(await RunAsync(async session =>
        {
            await session.App.OpenAsync();
            await session.Browser.GotoAsync("view-source:" + marker);
            Assert.DoesNotContain("marker-local-file", await session.Browser.EvaluateAsync<string>("() => document.body?.innerText ?? ''"));
        }));
        Assert.Equal("POLICY_DENIED", navigated.Code);
        Assert.Equal("forbidden URL scheme: view-source:", navigated.Message);

        Assert.Null(await RunAsync(async session =>
        {
            await session.App.OpenAsync();
            await session.App.OpenAsync("about:blank");
            await session.Browser.GotoAsync("about:blank");
            Assert.Equal("about:blank", await session.Browser.UrlAsync());
        }));

        var cookie = Assert.IsType<TestException>(await RunAsync(async session =>
        {
            await session.App.OpenAsync();
            await session.Browser.SetCookiesAsync([new BrowserCookie { Name = "flavor", Value = "oatmeal", Url = "about:blank" }]);
        }));
        Assert.Equal("POLICY_DENIED", cookie.Code);
        Assert.Equal("cookie URL must be http(s): about:blank", cookie.Message);
        directory.Delete(recursive: true);
    }

    // The upstream kitchen-sink tests these assertions read, by title.
    private static readonly Dictionary<string, Func<E2ESession, Task>> Scenarios = new()
    {
        ["reads wait for a node that arrives late"] = async session =>
        {
            await session.App.OpenAsync("/classes");
            Assert.Equal("card late", await session.Browser.Locator("#late-card").GetAttributeAsync("class"));
            Assert.Equal("Late card", await session.Screen.GetByText("Late card").TextContentAsync());
        },
        ["class assertions report the observed class on failure"] = async session =>
        {
            await session.App.OpenAsync();
            var failure = await Assert.ThrowsAsync<TestException>(() => Expect.That(session.Browser).ToHaveClassAsync(session.Browser.Locator("#class-card"), "card inactive", TimeSpan.FromMilliseconds(300)));
            Assert.Contains("class \"card active\"", failure.Message, StringComparison.Ordinal);
        },
        ["class assertions poll for a node that arrives late"] = async session =>
        {
            await session.App.OpenAsync("/classes");
            await Expect.That(session.Browser).ToHaveClassAsync(session.Browser.Locator("#late-card"), "card late", TimeSpan.FromMilliseconds(1800));
        },
        ["an empty class attribute is an empty class list"] = async session =>
        {
            await session.App.OpenAsync("/classes");
            await Expect.That(session.Browser).ToHaveClassAsync(session.Browser.Locator("#blank-card"), "");
        },
        ["reads under an absent frame answer with no match"] = async session =>
        {
            await session.App.OpenAsync("/frame");
            var absent = session.Browser.FrameLocator("#absent").GetByRole("button");
            Assert.Equal(0, await absent.CountAsync());
            Assert.Empty(await absent.AllAsync());
            Assert.Empty(await absent.AllTextContentsAsync());
            Assert.False(await absent.IsVisibleAsync());
            Assert.True(await absent.IsHiddenAsync());
        },
        ["a nested absent frame counts as zero matches at once"] = async session =>
        {
            await session.App.OpenAsync("/frame-nested");
            Assert.Equal(0, await session.Browser.FrameLocator("#outer").FrameLocator("#absent").GetByRole("button").CountAsync());
        },
        ["absence matchers and waits under an absent frame pass at once"] = async session =>
        {
            await session.App.OpenAsync("/frame");
            var absent = session.Browser.FrameLocator("#absent").GetByRole("button");
            var wait = TimeSpan.FromSeconds(3);
            await Expect.That(absent).ToBeAttachedAsync(attached: false, timeout: wait);
            await Expect.That(absent).ToBeVisibleAsync(visible: false, timeout: wait);
            await Expect.That(absent).ToBeHiddenAsync(timeout: wait);
            await absent.WaitForAsync(new LocatorWaitForOptions { State = WaitForState.Detached, Timeout = wait });
            await absent.WaitForAsync(new LocatorWaitForOptions { State = WaitForState.Hidden, Timeout = wait });
        },
        ["a *css= capture is refused before the last selector part"] = async session =>
        {
            await session.App.OpenAsync();
            await Expect.That(session.Browser.Locator("css=ul[data-testid=\"items\"] >> *css=li")).ToHaveCountAsync(3);
            var capture = "no error";
            try
            {
                await session.Browser.Locator("*css=ul[data-testid=\"items\"] >> li").CountAsync();
            }
            catch (Exception ex)
            {
                capture = ex.Message;
            }

            Assert.Contains("a selector that captures with * (*css=article >> text=Hello) is not supported", capture, StringComparison.Ordinal);
        },
        ["toHaveURL ignoreCase folds the comparison"] = async session =>
        {
            await session.App.OpenAsync("/classes");
            await Expect.That(session.Browser).ToHaveURLAsync("/CLASSES", ignoreCase: true);
            await Expect.That(session.Browser).ToHaveURLAsync(new Regex("CLASSES$"), ignoreCase: true);
            await Expect.That(session.Browser).Not.ToHaveURLAsync("/CLASSES", timeout: TimeSpan.FromMilliseconds(300));
        },
        ["string and RegExp URL patterns match what they name"] = async session =>
        {
            var browser = session.Browser;
            var flags = session.Screen.GetByRole("status", "Flags");
            var short200 = TimeSpan.FromMilliseconds(200);
            await session.App.OpenAsync("/about");
            await Expect.That(browser).ToHaveURLAsync("/about");
            await Expect.That(browser).ToHaveURLAsync(new Regex("/about$"));
            await Expect.That(browser).Not.ToHaveURLAsync("/other", timeout: short200);
            await Expect.That(browser).Not.ToHaveURLAsync(new Regex("/other$"), timeout: short200);
            await browser.WaitForURLAsync(new Regex("/about$"));
            await browser.RouteAsync(new Regex("/other$"), route => route.AbortAsync());
            await browser.RouteAsync(new Regex("/api/flags$"), route => route.FulfillAsync(new RouteFulfillResponse { Json = new Dictionary<string, bool> { ["betaBoard"] = true } }));
            var waiting = browser.WaitForResponseAsync(new Regex("/api/flags$"));
            await browser.GotoAsync("/flags");
            var response = await waiting;
            Assert.Matches("/api/flags$", response.Url);
            Assert.Equal(new Dictionary<string, bool> { ["betaBoard"] = true }, await response.JsonAsync<Dictionary<string, bool>>());
            await Expect.That(flags).ToHaveTextAsync("beta on");
            await browser.UnrouteAsync(new Regex("/api/flags$"));
            var real = browser.WaitForResponseAsync("**/api/flags");
            await browser.ReloadAsync();
            Assert.Equal(new Dictionary<string, bool> { ["betaBoard"] = false }, await (await real).JsonAsync<Dictionary<string, bool>>());
            await Expect.That(flags).ToHaveTextAsync("beta off");
        },
        ["routes are attempt-scoped: registered before the first page, kept across restart and clearState"] = async session =>
        {
            var flags = session.Screen.GetByRole("status", "Flags");
            await session.Browser.RouteAsync("**/api/flags", route => route.FulfillAsync(new RouteFulfillResponse { Json = new Dictionary<string, bool> { ["betaBoard"] = true } }));
            await session.App.OpenAsync("/flags");
            await Expect.That(flags).ToHaveTextAsync("beta on");
            await session.App.RestartAsync();
            await session.Browser.GotoAsync("/flags");
            await Expect.That(flags).ToHaveTextAsync("beta on");
            await session.App.ClearStateAsync();
            await session.Browser.GotoAsync("/flags");
            await Expect.That(flags).ToHaveTextAsync("beta on");
        },
        ["deterministic queries and reads"] = async session =>
        {
            var screen = session.Screen;
            await session.App.OpenAsync();

            await Expect.That(screen.GetByRole("heading", "Home")).ToBeVisibleAsync();
            await Expect.That(screen.GetByLabel("Email")).ToBeVisibleAsync();
            await Expect.That(screen.GetByPlaceholder("you@example.test")).ToBeVisibleAsync();
            await Expect.That(screen.GetByText("Item Alpha")).ToBeVisibleAsync();
            await Expect.That(screen.GetByDisplayValue("hello-value")).ToBeVisibleAsync();
            await Expect.That(screen.GetByTestId("items")).ToBeVisibleAsync();

            await Expect.That(screen.GetByTestId("item")).ToHaveCountAsync(3);
            await Expect.That(screen.GetByTestId("item").Filter("Beta")).ToHaveCountAsync(1);

            Assert.Equal(3, await screen.GetByTestId("item").CountAsync());

            var texts = new List<string>();
            foreach (var item in await screen.GetByTestId("item").AllAsync())
            {
                texts.Add(await item.TextContentAsync() ?? string.Empty);
            }

            Assert.Equal(["Item Alpha", "Item Beta", "Item Gamma"], texts);
            Assert.Equal(texts, await screen.GetByTestId("item").AllTextContentsAsync());
            Assert.Empty(await screen.GetByTestId("missing").AllAsync());
            await Expect.That(screen.GetByTestId("item")).ToHaveTextAsync(["Item Alpha", new Regex("Beta"), "Item Gamma"]);
            await Expect.That(screen.GetByTestId("item")).ToContainTextAsync(["Alpha", "Beta", "Gamma"]);
            await Expect.That(screen.GetByTestId("item")).ToContainTextAsync(["Alpha", "Gamma"]);
            await Expect.That(screen.GetByTestId("item")).Not.ToContainTextAsync(["Gamma", "Alpha"], timeout: TimeSpan.FromMilliseconds(200));
            await Expect.That(screen.GetByTestId("item")).Not.ToHaveTextAsync(["Item Alpha", "Item Beta"], timeout: TimeSpan.FromMilliseconds(200));

            Assert.Null(await screen.GetByTestId("items").GetAttributeAsync("class"));
            Assert.Equal("card active", await session.Browser.Locator("#class-card").GetAttributeAsync("class"));
            Assert.Equal(string.Empty, await screen.GetByLabel("Readonly").GetAttributeAsync("readonly"));
            Assert.Equal("/fixture.png", await session.Browser.Locator("#fixture-image").GetAttributeAsync("src"));
            Assert.Equal("own", await session.Browser.Locator("#class-card").GetAttributeAsync("constructor"));
            Assert.Null(await screen.GetByTestId("items").GetAttributeAsync("constructor"));
            Assert.Null(await screen.GetByTestId("items").GetAttributeAsync("toString"));
            Assert.Null(await screen.GetByTestId("items").GetAttributeAsync("__proto__"));
        },
        ["contenteditable hosts are textboxes: reached by label and by role (a bare host too), filled, and read as a value"] = async session =>
        {
            await session.App.OpenAsync("/editor");
            var notes = session.Screen.GetByLabel("Notes");
            await Expect.That(notes).ToBeVisibleAsync();
            await notes.FillAsync("Hello");
            await Expect.That(notes).ToHaveValueAsync("Hello");
            Assert.Equal("Hello", await notes.InputValueAsync());
            await Expect.That(session.Screen.GetByTestId("notes")).ToHaveValueAsync("Hello");
            // Upstream's getByRole runs Playwright's role selector, which knows a textbox only by an explicit role, so it
            // finds no bare host. The port's getByRole reads the tree, where the bare host is a textbox too.
            await Expect.That(session.Screen.GetByRole("textbox", "Notes")).ToHaveCountAsync(1);
            await Expect.That(session.Screen.GetByRole("textbox", "Message")).ToHaveCountAsync(1);
            await session.Screen.GetByRole("textbox", "Message").FillAsync("Hi");
            await Expect.That(session.Screen.GetByLabel("Message")).ToHaveValueAsync("Hi");
        },
        ["a textarea value compares raw"] = async session =>
        {
            await session.App.OpenAsync();
            await Expect.That(session.Screen.GetByLabel("Notes")).ToHaveValueAsync("line1\n\nline2  ");
            await Expect.That(session.Screen.GetByLabel("Notes")).ToHaveValueAsync(new Regex("^line1\n\nline2 {2}$"));
            await Expect.That(session.Screen.GetByLabel("Notes")).Not.ToHaveValueAsync("line1 line2", TimeSpan.FromMilliseconds(200));
        },
        ["a textarea value is never normalized to match"] = async session =>
        {
            await session.App.OpenAsync();
            await Expect.That(session.Screen.GetByLabel("Notes")).ToHaveValueAsync("line1 line2", TimeSpan.FromMilliseconds(500));
        },
        ["toHaveAttribute reads the value attribute of plain fields"] = async session =>
        {
            await session.App.OpenAsync("/value-attributes");
            await Expect.That(session.Screen.GetByLabel("Plain")).ToHaveAttributeAsync("value", "marker-5e0c");
            await Expect.That(session.Screen.GetByLabel("Blank")).Not.ToHaveAttributeAsync("value", TimeSpan.FromMilliseconds(200));
        },
        ["toHaveAttribute never judges a secure field"] = async session =>
        {
            await session.App.OpenAsync("/value-attributes");
            await Expect.That(session.Screen.GetByLabel("Secret")).ToHaveAttributeAsync("value");
        },
        ["a negated toHaveAttribute never judges a secure field"] = async session =>
        {
            await session.App.OpenAsync("/value-attributes");
            await Expect.That(session.Screen.GetByLabel("Secret")).Not.ToHaveAttributeAsync("value");
        },
        ["a negated toHaveAttribute fails on a plain field that holds the same marker"] = async session =>
        {
            await session.App.OpenAsync("/value-attributes");
            await Expect.That(session.Screen.GetByLabel("Plain")).Not.ToHaveAttributeAsync("value", TimeSpan.FromMilliseconds(500));
        },
        ["secure fields refuse value reads"] = async session =>
        {
            await session.App.OpenAsync();
            await session.Screen.GetByLabel("Password").FillAsync("hunter2");
            await session.Screen.GetByLabel("Password").InputValueAsync();
        },
        ["toHaveValue never judges a secure field"] = async session =>
        {
            await session.App.OpenAsync();
            await session.Screen.GetByLabel("Password").FillAsync("marker-9b1f");
            await Expect.That(session.Screen.GetByLabel("Password")).ToHaveValueAsync(string.Empty);
        },
        ["toHaveValue reads a plain field that holds the same marker"] = async session =>
        {
            await session.App.OpenAsync();
            await session.Screen.GetByLabel("Email").FillAsync("marker-9b1f");
            await Expect.That(session.Screen.GetByLabel("Email")).ToHaveValueAsync(string.Empty, TimeSpan.FromMilliseconds(500));
        },
        ["a viewport set before the first navigation holds through app.open"] = async session =>
        {
            await session.Browser.SetViewportAsync(390, 600);
            await session.App.OpenAsync();
            Assert.Equal(390, await session.Browser.EvaluateAsync<int>("() => window.innerWidth"));
            await session.App.RestartAsync();
            Assert.Equal(390, await session.Browser.EvaluateAsync<int>("() => window.innerWidth"));
            await session.App.ClearStateAsync();
            await session.App.OpenAsync();
            Assert.Equal(390, await session.Browser.EvaluateAsync<int>("() => window.innerWidth"));
        },
        ["a viewport set before the first navigation opens no page"] = async session =>
        {
            await session.Browser.SetViewportAsync(390, 600);
            await session.Screen.GetByTestId("items").TapAsync(new ActionOptions { Timeout = TimeSpan.FromMilliseconds(200) });
        },
        ["forbidden URL schemes are refused"] = async session =>
        {
            await session.App.OpenAsync();
            await session.App.OpenAsync("javascript:alert(1)");
        },
        ["a route handler assertion that no step follows"] = async session =>
        {
            await session.Browser.RouteAsync("**/api/flags.js", async route =>
            {
                await route.FulfillAsync(new RouteFulfillResponse { Body = "", ContentType = "text/javascript" });
                ExpectToBe(route.Request.Method, "POST");
            });
            await session.App.OpenAsync("/script-flags");
        },
        ["a dialog handler assertion that no step follows"] = async session =>
        {
            await session.App.OpenAsync("/dialog");
            await session.Browser.OnDialogAsync(async dialog =>
            {
                await dialog.AcceptAsync();
                ExpectToBe(dialog.Message, "Are you sure?");
            });
            await session.Screen.GetByRole("button", "Ask").TapAsync();
        },
    };

    // The port has no value matcher (upstream's expect(value).toBe), so a handler's assertion throws the ASSERTION_FAILED it would.
    private static void ExpectToBe(string actual, string expected)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new TestException("ASSERTION_FAILED", $"expect(received).toBe(expected)\nExpected: \"{expected}\"\nReceived: \"{actual}\"");
        }
    }

    // Runs one upstream test body in its own session and returns what failed it, as the upstream run's result does.
    private static async Task<Exception?> RunAsync(Func<E2ESession, Task> body) => (await RunWithStepsAsync(body)).Error;

    // RunAsync, with the steps the attempt recorded, as the upstream run's result keeps them.
    private static async Task<(Exception? Error, IReadOnlyList<StepRecord> Steps)> RunWithStepsAsync(Func<E2ESession, Task> body)
    {
        using var site = await FixtureApp.StartAsync();
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(headless: true),
            Model = new ScriptedModel(_ => ModelResponses.Done("passed", "unused")),
            BaseUrl = site.Url,
            TestTitle = "web platform",
            ActionTimeout = TimeSpan.FromSeconds(5),
            AssertionTimeout = TimeSpan.FromSeconds(4),
            TestTimeout = TimeSpan.FromSeconds(30),
        });
        Exception? error = null;
        try
        {
            await body(session);
        }
        catch (Exception ex)
        {
            error = ex;
        }

        // The attempt end: a handler that failed with no step after it fails the attempt, as the runner's settleAttempt does.
        try
        {
            await session.SettleAttemptAsync();
        }
        catch (Exception ex)
        {
            error ??= ex;
        }

        session.Complete(error);
        return (error, session.StepRecords.ToList());
    }
}
