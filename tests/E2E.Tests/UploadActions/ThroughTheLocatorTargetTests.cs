// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.CheckActions.CheckPage;
using static E2E.Tests.UploadActions.UploadPage;

namespace E2E.Tests.UploadActions;

/// <summary>
/// <c>setInputFiles</c> through a screen locator, ported from upstream <c>upload-actions.test.ts</c> (<c>describe.each</c> over
/// the <c>locator</c> target). Upstream's locator is a CSS selector; this port finds the control by label, test id, or role; the label's own text is a span with a test id, since the observation lists no node for a label.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class ThroughTheLocatorTargetTests
{
    [Fact]
    public async Task Sets_a_file_input_directly()
    {
        await RunWithScreenAsync(
            $"""<input id="avatar" type="file" aria-label="Avatar">{CountClicks}""",
            async session =>
            {
                await session.Screen.GetByLabel("Avatar").SetInputFilesAsync(AvatarFile);
                Assert.Equal("avatar.png|0", await session.Browser.EvaluateAsync<string>(State));
            }
        );
    }

    [Theory]
    [InlineData("input")]
    [InlineData("label")]
    [InlineData("chooser")]
    public async Task Partial_Preserves_in_memory_file_metadata_and_binary_contents_through_a(string kind)
    {
        await RunWithScreenAsync(MetadataPage(kind), async session =>
        {
            var target = kind switch
            {
                "input" => session.Screen.GetByLabel("Avatar"),
                "label" => session.Screen.GetByTestId("pick"),
                _ => session.Screen.GetByRole("button", "Choose"),
            };
            await target.SetInputFilesAsync(Payloads);
            Assert.Equal(PayloadsExpected, await session.Browser.EvaluateAsync<string>(PayloadsHeld));
            await target.SetInputFilesAsync([]);
            Assert.Equal(
                0,
                await session.Browser.EvaluateAsync<int>("() => document.querySelector('#avatar').files.length")
            );
        });
    }

    [Fact]
    public async Task Sets_the_input_a_label_names_without_clicking_the_label()
    {
        await RunWithScreenAsync(
            $"""<label id="pick" for="avatar"><span data-testid="pick">Choose avatar</span></label><input id="avatar" type="file" hidden>{CountClicks}""",
            async session =>
            {
                await session.Screen.GetByTestId("pick").SetInputFilesAsync(AvatarFile);
                Assert.Equal("avatar.png|0", await session.Browser.EvaluateAsync<string>(State));
            }
        );
    }

    [Fact]
    public async Task Clicks_a_button_over_a_hidden_input_and_sets_the_files_through_its_chooser()
    {
        await RunWithScreenAsync(
            $"""
            <button id="pick" type="button" onclick="document.getElementById('avatar').click()">Choose avatar</button>
            <input id="avatar" type="file" hidden>{CountClicks}
            """,
            async session =>
            {
                await session.Screen.GetByRole("button", "Choose avatar").SetInputFilesAsync(AvatarFile);
                Assert.Equal("avatar.png", (await session.Browser.EvaluateAsync<string>(State))!.Split('|')[0]);
            }
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Fails_a_path_that_does_not_exist_as_INVALID_ARGUMENT_before_it_touches_a_file_input_or_a_button_over_a_hidden_input(
        bool button
    )
    {
        await RunWithScreenAsync(
            (
                button
                    ? """<button id="pick" type="button" onclick="document.getElementById('avatar').click()">Choose avatar</button><input id="avatar" type="file" hidden>"""
                    : """<input id="avatar" type="file" aria-label="Avatar">"""
            ) + CountClicks,
            async session =>
            {
                var target = button
                    ? session.Screen.GetByRole("button", "Choose avatar")
                    : session.Screen.GetByLabel("Avatar");
                foreach (var missing in MissingPaths())
                {
                    var error = await Assert.ThrowsAsync<TestException>(() =>
                        target.SetInputFilesAsync([AvatarFile, missing])
                    );
                    Assert.Equal("INVALID_ARGUMENT", error.Code);
                    Assert.Equal(MissingPathMessage + StatFailure(missing), error.Message);
                    Assert.Contains(missing, error.Message, StringComparison.Ordinal);
                }

                Assert.Equal("|0", await session.Browser.EvaluateAsync<string>(State));
            }
        );
    }

    [Fact]
    public async Task Fails_a_path_under_a_directory_it_may_not_search_as_INVALID_ARGUMENT()
    {
        // Permission bits do not stop a Windows or root process, so only a non-root POSIX runner sees the denial.
        if (!TryLockDirectory("locked-locator", out var locked, out var hidden))
        {
            return;
        }

        try
        {
            await RunWithScreenAsync(
                $"""<input id="avatar" type="file" aria-label="Avatar">{CountClicks}""",
                async session =>
                {
                    var error = await Assert.ThrowsAsync<TestException>(() =>
                        session.Screen.GetByLabel("Avatar").SetInputFilesAsync(hidden)
                    );
                    Assert.Equal("INVALID_ARGUMENT", error.Code);
                    Assert.Equal(MissingPathMessage + StatFailure(hidden), error.Message);
                    Assert.Equal("|0", await session.Browser.EvaluateAsync<string>(State));
                }
            );
        }
        finally
        {
            UnlockDirectory(locked);
        }
    }

    [Fact]
    public async Task Fails_when_the_button_opens_no_file_chooser()
    {
        await RunWithScreenAsync(
            $"""<button id="pick" type="button">Choose avatar</button><input id="avatar" type="file" hidden>{CountClicks}""",
            async session =>
            {
                var failure = await Assert.ThrowsAsync<TestException>(() =>
                    session.Screen.GetByRole("button", "Choose avatar").SetInputFilesAsync(AvatarFile)
                );
                var error = Assert.IsType<EngineException>(failure.InnerException);
                Assert.Equal("ACTION_FAILED", failure.Code);
                Assert.Equal("NOT_ACTIONABLE", error.Code);
                Assert.Contains("opened no file chooser", error.Message, StringComparison.Ordinal);
                Assert.Equal("", (await session.Browser.EvaluateAsync<string>(State))!.Split('|')[0]);
            }
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Clears_a_file_input_or_a_button_over_a_hidden_input_with_an_empty_list_and_fires_input_and_change(
        bool button
    )
    {
        await RunWithScreenAsync(
            (
                button
                    ? """<button id="pick" type="button" onclick="document.getElementById('avatar').click()">Choose avatar</button><input id="avatar" type="file" hidden>"""
                    : """<input id="avatar" type="file" aria-label="Avatar">"""
            ) + CountClicks,
            async session =>
            {
                var target = button
                    ? session.Screen.GetByRole("button", "Choose avatar")
                    : session.Screen.GetByLabel("Avatar");
                await target.SetInputFilesAsync(AvatarFile);
                await session.Browser.EvaluateAsync<string>(ListenForInputAndChange);
                await target.SetInputFilesAsync([]);
                Assert.Equal("", (await session.Browser.EvaluateAsync<string>(State))!.Split('|')[0]);
                Assert.Equal(
                    "input,change",
                    await session.Browser.EvaluateAsync<string>("() => window.events.join(',')")
                );
            }
        );
    }
}
