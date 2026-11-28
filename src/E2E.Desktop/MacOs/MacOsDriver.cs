// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Reflection;
using E2E.Desktop.MacOs;

namespace E2E.Desktop;

/// <summary>
/// The macOS accessibility driver of the desktop engine: accessibility, input, launch, and capture, called in process through
/// P/Invoke into the system frameworks. Port of the <c>e2e-desktop-napi</c> crate (<c>lib.rs</c>), which upstream loads into Node
/// through NAPI-RS. Every call that talks to another process runs on a pool thread, in an autorelease pool, and answers an
/// <see cref="Envelope{T}"/> (<c>{ ok, data }</c> or <c>{ ok, error }</c>), never an exception, so the engine maps each failure onto
/// its error contract. Constructing it on any other operating system throws <see cref="PlatformNotSupportedException"/>.
/// </summary>
public sealed class MacOsDriver : IDesktopDriver
{
    /// <summary>How long one request to another app may take before it gives up, in seconds.</summary>
    private const float MessagingTimeoutSeconds = 1.0f;

    private static readonly Lazy<bool> s_initialized = new(() =>
    {
        Element.SetGlobalTimeout(MessagingTimeoutSeconds);
        return true;
    });

    /// <summary>Creates the driver. <paramref name="version"/> is the version of the package it ships in, which the loader reads; the assembly's version by default.</summary>
    /// <exception cref="PlatformNotSupportedException">This is not macOS: the driver is built for nothing else.</exception>
    public MacOsDriver(string? version = null)
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException(
                $"the macOS desktop driver drives macOS apps only; this host is {System.Runtime.InteropServices.RuntimeInformation.OSDescription}"
            );
        }

        Version = version
            ?? typeof(MacOsDriver)
                .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
            ?? "unknown";
    }

    /// <inheritdoc />
    public string Version { get; }

    private static void Init() => _ = s_initialized.Value;

    private static Instant Deadline(double timeoutMs) =>
        Instant.After(TimeSpan.FromMilliseconds(Math.Max(timeoutMs, 0.0)));

    /// <summary>
    /// Runs <paramref name="work"/> on a thread of its own and answers its envelope. The work runs in an autorelease pool: such a thread has
    /// none, and the AppKit and Foundation calls a launch makes would otherwise leak what they autorelease for as long as the worker
    /// lives.
    /// </summary>
    private static Task<Envelope<T>> Blocking<T>(Func<T> work) =>
        // A thread of its own, as Rust's blocking pool gives: a call sleeps and polls the app for a second or more, and a pool of
        // few threads would starve under the engine's concurrent calls.
        Task.Factory.StartNew(
            () =>
            {
                Init();
                try
                {
                    return Envelope<T>.Success(ObjC.InAutoreleasePool(work));
                }
                catch (Failure failure)
                {
                    return Envelope<T>.Failure(failure.ToError());
                }
                catch (Exception error)
                {
                    return Envelope<T>.Failure(
                        Failure.Unconfirmed(Codes.Internal, $"the native call failed: {error.Message}").ToError()
                    );
                }
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        );

    private static uint WindowId(string id) =>
        uint.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var window)
            ? window
            : throw Failure.Refused(Codes.WindowNotFound, $"\"{id}\" is not a window id");

    /// <inheritdoc />
    /// <remarks>The permissions this process holds.</remarks>
    public DriverStatus Status()
    {
        Init();
        return new DriverStatus(Element.Trusted(), SystemInfo.ScreenRecording());
    }

    /// <inheritdoc />
    public bool? ScreenLocked() => SystemInfo.ScreenLocked();

    /// <inheritdoc />
    public long? ProcessStarted(int pid) => ProcessInfo.Started(pid);

    /// <inheritdoc />
    /// <remarks>It reads every process, which takes tens of milliseconds, so it runs on a pool thread.</remarks>
    public async Task<IReadOnlyList<int>?> ProcessesCarryingAsync(string token)
    {
        try
        {
            return await Task.Run(() => ProcessInfo.Carrying(token)).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void ForgetProcess(int pid) => Refs.ForgetProcess(pid);

    /// <inheritdoc />
    public Task<Envelope<Launched>> LaunchAsync(LaunchRequest request)
    {
        // The budget counts from the call, not from when a pool thread picks the work up.
        var asked = Instant.Now;
        return Blocking(() =>
        {
            var environment = request
                .Environment.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ThenBy(pair => pair.Value, StringComparer.Ordinal)
                .ToList();
            return MacOs.Launch.Start(
                new MacOs.Launch.Request
                {
                    Path = request.Path,
                    BundleId = request.BundleId,
                    Arguments = request.Arguments,
                    Environment = environment,
                    Token = request.Token,
                    Activate = request.Activate,
                    Timeout = TimeSpan.FromMilliseconds(Math.Max(request.TimeoutMs, 0)),
                    Claim = TimeSpan.FromMilliseconds(Math.Max(request.ClaimMs, 0)),
                    Asked = asked,
                    InputId = request.InputId,
                }
            );
        });
    }

    /// <inheritdoc />
    public Task<Envelope<IReadOnlyList<RawWindow>>> ListWindowsAsync(int pid) =>
        Blocking<IReadOnlyList<RawWindow>>(() => Windows.List(pid));

    /// <inheritdoc />
    public Task<Envelope<RawSnapshot>> SnapshotAsync(SnapshotRequest request)
    {
        var deadline = Deadline(request.TimeoutMs);
        return Blocking(() =>
            Snapshot.Take(
                new Snapshot.Request(
                    request.Pid,
                    WindowId(request.WindowId),
                    Math.Max(request.MaxDepth, 0),
                    request.Interactive,
                    deadline
                )
            )
        );
    }

    /// <inheritdoc />
    public Task<Envelope<Nothing>> ActAsync(ActRequest request)
    {
        var deadline = Deadline(request.TimeoutMs);
        return Blocking(() =>
        {
            string Text() =>
                request.Value ?? throw Failure.Refused(Codes.InvalidArgs, $"{request.Action} needs a value");
            DriverAction action = request.Action switch
            {
                "click" => new DriverAction.Click(),
                "doubleClick" => new DriverAction.DoubleClick(),
                "rightClick" => new DriverAction.RightClick(),
                "focus" => new DriverAction.Focus(),
                "clear" => new DriverAction.Clear(),
                "check" => new DriverAction.Check(true),
                "uncheck" => new DriverAction.Check(false),
                "scrollTo" => new DriverAction.ScrollTo(),
                "hover" => new DriverAction.Hover(),
                "setValue" => new DriverAction.SetValue(Text()),
                "selectAll" => new DriverAction.SelectAll(),
                "select" => new DriverAction.Select(Text()),
                "scroll" => new DriverAction.Scroll(request.Direction ?? "down", request.Pages ?? 0.5),
                "type" => new DriverAction.Type(Text(), request.Physical ?? false),
                var other => throw Failure.Refused(Codes.InvalidArgs, $"no action \"{other}\""),
            };
            Actions.Act(request.Ref, action, request.InputId, deadline);
            return Nothing.Value;
        });
    }

    /// <inheritdoc />
    /// <remarks>Unique across every worker of the process, which share the driver's cancel marks.</remarks>
    public uint InputId() => Input.NextInputId();

    /// <inheritdoc />
    public void CancelInput(uint inputId) => Input.CancelInput(inputId);

    /// <inheritdoc />
    public Task<Envelope<Nothing>> CheckComboAsync(string combo) =>
        Blocking(() =>
        {
            Input.ParseCombo(combo);
            return Nothing.Value;
        });

    /// <inheritdoc />
    public Task<Envelope<Nothing>> PressAsync(PressRequest request)
    {
        var deadline = Deadline(request.TimeoutMs);
        return Blocking(() =>
        {
            Actions.Press(
                request.Pid,
                WindowId(request.WindowId),
                request.Combo,
                request.Physical,
                request.FocusedRef,
                request.InputId,
                deadline
            );
            return Nothing.Value;
        });
    }

    /// <inheritdoc />
    /// <remarks>Brings a window and its app forward. <paramref name="inputId"/> names it, so <see cref="CancelInput"/> stops it before it changes the focus.</remarks>
    public Task<Envelope<Nothing>> FocusWindowAsync(
        int pid,
        string windowId,
        uint? inputId = null,
        int? timeoutMs = null
    )
    {
        var deadline = Deadline(timeoutMs ?? 60_000);
        return Blocking(() =>
        {
            Actions.FocusWindow(pid, WindowId(windowId), inputId, deadline);
            return Nothing.Value;
        });
    }

    /// <inheritdoc />
    /// <remarks>
    /// Physical pointer input at a point on screen: <c>tap</c>, <c>doubleTap</c>, <c>secondaryTap</c>, or <c>hover</c>. With a target, no
    /// click goes once another window, a new sheet, or another app took the focus from that window.
    /// </remarks>
    public Task<Envelope<Nothing>> PointerAsync(
        double x,
        double y,
        string kind,
        uint? inputId = null,
        int? timeoutMs = null,
        PointerTarget? target = null
    )
    {
        // No budget given: the call sends at once, as before `timeoutMs` was passed.
        var deadline = Deadline(timeoutMs ?? 60_000);
        return Blocking(() =>
        {
            MacOs.PointTarget? point = null;
            if (target is not null)
            {
                Sheet sheet = (target.Sheet, target.SheetId) switch
                {
                    (false, _) => new Sheet.None(),
                    (true, { } key) => new Sheet.Key(key),
                    (true, null) => new Sheet.Unnamed(),
                };
                point = new MacOs.PointTarget(target.Pid, WindowId(target.WindowId), sheet, target.Content ?? false);
            }

            Actions.PointerAt(x, y, kind, inputId, deadline, point);
            return Nothing.Value;
        });
    }

    /// <inheritdoc />
    public Task<Envelope<ScreenshotResult>> ScreenshotAsync(ScreenshotRequest request)
    {
        var deadline = Deadline(request.TimeoutMs);
        return Blocking(() =>
            new ScreenshotResult(SystemInfo.Screenshot(WindowId(request.WindowId), request.Path, deadline))
        );
    }
}
