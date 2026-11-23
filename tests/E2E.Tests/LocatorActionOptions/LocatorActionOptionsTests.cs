// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.LocatorActionOptions;

/// <summary>
/// "%s rejects an unknown option before any lookup, action, or step" is not ported: the port's options are a typed
/// class. The nested "modifiers" describe is <see cref="LocatorActionOptionsModifiersTests"/>.
/// </summary>
public sealed class LocatorActionOptionsTests
{
    private static readonly SemanticNode Box = new()
    {
        Ref = "agree",
        Role = "checkbox",
        Name = "Agree",
    };

    private static readonly SemanticNode Bin = new()
    {
        Ref = "bin",
        Role = "region",
        Name = "Bin",
    };

    private static readonly Dictionary<string, Func<Locator, ActionOptions, Screen, Task>> Calls = new()
    {
        ["tap"] = (locator, options, _) => locator.TapAsync(options),
        ["click"] = (locator, options, _) => locator.ClickAsync(options),
        ["doubleTap"] = (locator, options, _) => locator.DoubleTapAsync(options),
        ["dblclick"] = (locator, options, _) => locator.DblClickAsync(options),
        ["secondaryTap"] = (locator, options, _) =>
            locator.SecondaryTapAsync(new ClickOptions { Timeout = options.Timeout }),
        ["longPress"] = (locator, options, _) =>
            locator.LongPressAsync(new LongPressOptions { Timeout = options.Timeout }),
        ["fill"] = (locator, options, _) => locator.FillAsync("yes", options),
        ["clear"] = (locator, options, _) => locator.ClearAsync(options),
        ["press"] = (locator, options, _) => locator.PressAsync("Enter", options),
        ["check"] = (locator, options, _) => locator.CheckAsync(options),
        ["uncheck"] = (locator, options, _) => locator.UncheckAsync(options),
        ["selectOption"] = (locator, options, _) => locator.SelectOptionAsync("Team", options),
        ["focus"] = (locator, options, _) => locator.FocusAsync(options),
        ["hover"] = (locator, options, _) => locator.HoverAsync(options),
        ["setInputFiles"] = (locator, options, _) => locator.SetInputFilesAsync("fixtures/a.txt", options),
        ["dragTo"] = (locator, options, screen) => locator.DragToAsync(screen.GetByRole("region", "Bin"), options),
        ["scrollIntoView"] = (locator, options, _) => locator.ScrollIntoViewAsync(options),
        ["swipe"] = (locator, options, _) =>
            locator.SwipeAsync(new SwipeOptions { Direction = ScrollDirection.Down, Timeout = options.Timeout }),
    };

    public static TheoryData<string> Verbs => new(Calls.Keys);

    /// <summary>A screen over a checkbox and a drop region that keeps every action the engine receives. Modifiers are held when <paramref name="tapModifiers"/> is true.</summary>
    internal static (Screen Screen, List<LocatorAction> Received) BoxScreen(bool tapModifiers = false)
    {
        var received = new List<LocatorAction>();
        var observation = new Observation { Route = "/", Roots = [Box, Bin] };
        var screen = new Screen(
            _ => Task.FromResult(observation),
            (_, action, _) =>
            {
                received.Add(action);
                return Task.CompletedTask;
            },
            () => CancellationToken.None,
            () => { },
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1)
        )
        {
            Capabilities = tapModifiers ? EngineCapabilities.TapModifiers : EngineCapabilities.None,
            Steps = new StepRecorder("attempt"),
        };
        return (screen, received);
    }

    [Theory]
    [MemberData(nameof(Verbs))]
    public async Task Performs_with_a_timeout(string verb)
    {
        var (screen, performed) = BoxScreen();

        await Calls[verb](screen.GetByLabel("Agree"), new ActionOptions { Timeout = TimeSpan.FromSeconds(1) }, screen);

        Assert.Single(performed);
        var step = Assert.Single(screen.Steps!.All());
        Assert.Equal($"locator.{verb}", step.Api);
        Assert.Equal(StepStatus.Passed, step.Status);
    }

    [Fact]
    public async Task LongPress_sends_a_duration_from_100_through_10000_and_none_when_unset_so_the_engine_default_holds()
    {
        var (screen, received) = BoxScreen();
        var box = screen.GetByLabel("Agree");

        await box.LongPressAsync();
        await box.LongPressAsync(new LongPressOptions { Duration = TimeSpan.FromMilliseconds(100) });
        await box.LongPressAsync(new LongPressOptions { Duration = TimeSpan.FromMilliseconds(10_000) });

        Assert.Equal<LocatorAction>(
            [
                new LocatorAction.LongPress(),
                new LocatorAction.LongPress(TimeSpan.FromMilliseconds(100)),
                new LocatorAction.LongPress(TimeSpan.FromMilliseconds(10_000)),
            ],
            received
        );
        Assert.Null(((LocatorAction.LongPress)received[0]).Duration);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(10_001)]
    [InlineData(1.5)]
    public async Task LongPress_refuses_a_duration_of_x_before_any_action(double milliseconds)
    {
        var (screen, received) = BoxScreen();

        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            screen
                .GetByLabel("Agree")
                .LongPressAsync(new LongPressOptions { Duration = TimeSpan.FromMilliseconds(milliseconds) })
        );

        Assert.Contains("longPress duration must be an integer from 100 through 10000", error.Message);
        Assert.Empty(received);
    }

    [Fact]
    public async Task SetInputFiles_hands_an_empty_list_to_the_engine_which_clears_the_input()
    {
        var (screen, received) = BoxScreen();

        await screen.GetByLabel("Agree").SetInputFilesAsync([]);

        Assert.Empty(Assert.IsType<LocatorAction.SetInputFiles>(Assert.Single(received)).Paths);
        var step = Assert.Single(screen.Steps!.All());
        Assert.Equal("locator.setInputFiles", step.Api);
        Assert.Equal(StepStatus.Passed, step.Status);
    }

    [Fact]
    public async Task Rejects_payload_size_at_or_above_50_MiB_before_lookup_dispatch_or_recording()
    {
        var (screen, received) = BoxScreen();
        const int limit = 50 * 1024 * 1024;
        var buffer = new byte[limit + 1];
        var file = new FilePayload("large.bin", "application/octet-stream", buffer);
        var oversized = new (string Case, Func<Task> Call)[]
        {
            ("one file at the limit", () =>
                screen.GetByLabel("Agree").SetInputFilesAsync(file with { Buffer = buffer[..limit] })
            ),
            ("one file over the limit", () => screen.GetByLabel("Agree").SetInputFilesAsync(file)),
            ("two files that add up to the limit", () =>
                screen
                    .GetByLabel("Agree")
                    .SetInputFilesAsync([
                        file with
                        {
                            Buffer = buffer[..(limit / 2)],
                        },
                        file with
                        {
                            Buffer = buffer[(limit / 2)..limit],
                        },
                    ])
            ),
        };

        foreach (var (_, call) in oversized)
        {
            var error = await Assert.ThrowsAsync<TestException>(call);
            Assert.Equal("INVALID_ARGUMENT", error.Code);
            Assert.Equal(
                "Cannot set buffer larger than 50Mb, please write it to a file and pass its path instead.",
                error.Message
            );
        }

        Assert.Empty(received);
        Assert.Empty(screen.Steps!.All());
    }

    [Fact]
    public async Task Accepts_payload_size_just_below_50_MiB()
    {
        var (screen, received) = BoxScreen();

        await screen
            .GetByLabel("Agree")
            .SetInputFilesAsync(
                new FilePayload("large.bin", "application/octet-stream", new byte[(50 * 1024 * 1024) - 1])
            );

        var action = Assert.IsType<LocatorAction.SetInputFilePayloads>(Assert.Single(received));
        Assert.Equal((50 * 1024 * 1024) - 1, Assert.Single(action.Files).Buffer.Length);
    }

    // Not portable, because the C# types refuse it at compile time: "rejects mixed file paths and buffers in either order".

    [Fact]
    public async Task Snapshots_single_files_and_arrays_as_bytes_without_putting_contents_in_steps()
    {
        var (screen, received) = BoxScreen();
        var file = new FilePayload("receipt.bin", "application/octet-stream", [0, 128, 255]);

        var pending = screen.GetByLabel("Agree").SetInputFilesAsync(file);
        file.Buffer[0] = 99;
        await pending;
        file.Buffer[0] = 0;
        await screen.GetByLabel("Agree").SetInputFilesAsync([file, file with { Name = "empty.bin", Buffer = [] }]);

        var first = Assert.IsType<LocatorAction.SetInputFilePayloads>(received[0]);
        var second = Assert.IsType<LocatorAction.SetInputFilePayloads>(received[1]);
        Assert.Equal(2, received.Count);
        Assert.Equal<byte>([0, 128, 255], Assert.Single(first.Files).Buffer);
        Assert.Equal("receipt.bin", first.Files[0].Name);
        Assert.Equal("application/octet-stream", first.Files[0].MimeType);
        Assert.Equal<byte>([0, 128, 255], second.Files[0].Buffer);
        Assert.Equal("empty.bin", second.Files[1].Name);
        Assert.Empty(second.Files[1].Buffer);
        file.Buffer[0] = 99;
        Assert.Equal<byte>([0, 128, 255], first.Files[0].Buffer);
        Assert.DoesNotContain("AID/", System.Text.Json.JsonSerializer.Serialize(screen.Steps!.All()));
    }

    [Fact]
    public async Task Uploads_then_clears_on_a_payload_only_engine()
    {
        var (screen, received) = BoxScreen();
        screen.ActionKinds = ["setInputFilePayloads"];
        var input = screen.GetByLabel("Agree");

        await input.SetInputFilesAsync(new FilePayload("a.txt", "text/plain", "a"u8.ToArray()));
        var error = await Assert.ThrowsAsync<EngineException>(() => input.SetInputFilesAsync("a.txt"));
        Assert.Equal(EngineErrorCodes.UnsupportedCapability, error.Code);
        Assert.Single(received);
        await input.SetInputFilesAsync([]);

        Assert.Equal(2, received.Count);
        Assert.Empty(Assert.IsType<LocatorAction.SetInputFilePayloads>(received[1]).Files);
    }

    [Theory]
    [InlineData("setInputFiles")]
    [InlineData(
        "tap",
        "doubleTap",
        "secondaryTap",
        "longPress",
        "fill",
        "clear",
        "press",
        "check",
        "uncheck",
        "focus",
        "hover",
        "scrollIntoView",
        "selectOption",
        "setInputFiles",
        "dragTo",
        "swipe"
    )]
    public async Task Rejects_payload_uploads_when_the_engine_declares_no_payload_action(params string[] actions)
    {
        var (screen, received) = BoxScreen();
        screen.ActionKinds = actions;

        var error = await Assert.ThrowsAsync<EngineException>(() =>
            screen.GetByLabel("Agree").SetInputFilesAsync(new FilePayload("a.txt", "text/plain", "data"u8.ToArray()))
        );

        Assert.Equal(EngineErrorCodes.UnsupportedCapability, error.Code);
        Assert.Equal(
            "in-memory files for setInputFiles are not available on this target; update its engine or pass a file path",
            error.Message
        );
        Assert.Empty(received);
        await screen.GetByLabel("Agree").SetInputFilesAsync("a.txt");
        Assert.Single(Assert.IsType<LocatorAction.SetInputFiles>(Assert.Single(received)).Paths);
    }

    [Fact]
    public async Task Does_not_suggest_file_paths_when_the_engine_supports_no_uploads()
    {
        var (screen, received) = BoxScreen();
        screen.ActionKinds = ["tap"];

        var payload = await Assert.ThrowsAsync<EngineException>(() =>
            screen.GetByLabel("Agree").SetInputFilesAsync(new FilePayload("a.txt", "text/plain", "data"u8.ToArray()))
        );
        var clear = await Assert.ThrowsAsync<EngineException>(() => screen.GetByLabel("Agree").SetInputFilesAsync([]));

        Assert.Equal(EngineErrorCodes.UnsupportedCapability, payload.Code);
        Assert.Equal(
            "in-memory files for setInputFiles are not available on this target; update its engine",
            payload.Message
        );
        Assert.Equal(EngineErrorCodes.UnsupportedCapability, clear.Code);
        Assert.Equal(
            "the \"setInputFiles\" action is not available on this target: its engine declares tap",
            clear.Message
        );
        Assert.Empty(received);
    }

    // Not portable, because the C# types refuse them at compile time: "refuses invalid file payload %j before lookup or recording" for
    // null, 42, {}, a sparse array, a missing mimeType, a Uint8Array or a string buffer, a path mixed with a payload, and an unknown key.
    // The cases the types leave open are below.
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Partial_refuses_invalid_file_payload_before_lookup_or_recording(string name)
    {
        var (screen, received) = BoxScreen();

        var error = await Assert.ThrowsAsync<TestException>(() =>
            screen.GetByLabel("Agree").SetInputFilesAsync(new FilePayload(name, "text/plain", []))
        );

        Assert.Equal("INVALID_ARGUMENT", error.Code);
        Assert.Empty(received);
        Assert.Empty(screen.Steps!.All());
    }

    // Not portable, because the C# types refuse them at compile time: a number among the paths.
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("fixtures/a.txt", "")]
    public async Task SetInputFiles_refuses_paths_before_any_lookup_or_action(params string[] paths)
    {
        var (screen, received) = BoxScreen();

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            screen.GetByLabel("Agree").SetInputFilesAsync(paths)
        );

        Assert.Contains("setInputFiles paths must be non-empty strings", error.Message, StringComparison.Ordinal);
        Assert.Empty(received);
        Assert.Empty(screen.Steps!.All());
    }
}
