// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json.Nodes;
using E2E.Engine;
using E2E.Mobile;

namespace E2E.BrowserStack.Tests.Recording;

/// <summary>
/// Ports <c>describe('browserstack().record()')</c> of <c>packages/browserstack/tests/unit/recording.test.ts</c>: the build and session
/// it finds by name, the start time it takes from it, the video it links once the session ends, the endpoint override, and the failures
/// it names without leaking credentials or signed URLs, against a fake App Automate API.
/// </summary>
public sealed class BrowserStackRecordTests
{
    private const string Video = "https://app-automate.browserstack.com/sessions/S2/video?token=secret-token";

    private static readonly BrowserStackDevicesOptions Options = new() { Device = "Google Pixel 9", OsVersion = "16.0", App = "bs://APP1" };

    private static readonly Dictionary<string, string?> Env = new() { ["BROWSERSTACK_USERNAME"] = "ada", ["BROWSERSTACK_ACCESS_KEY"] = "bs-key" };

    private readonly FakeAppAutomate _api = new();

    private static DeviceLease Lease(string build = "run-1", string sessionName = "e2e-run-1-android-2") => new()
    {
        Id = "lease-1",
        Client = new JsonObject
        {
            ["stateDir"] = "/work/state",
            ["tenant"] = "browserstack",
            ["runId"] = "run-1",
            ["leaseId"] = "lease-1",
            ["providerBuild"] = build,
            ["providerSessionName"] = sessionName,
        },
    };

    public BrowserStackRecordTests()
    {
        _api.Builds =
        [
            Build("run-2", "B3"),
            Build("run-1", "B2"),
            Build("run-1", "B1"),
        ];
        _api.Sessions = new Dictionary<string, List<JsonObject>>
        {
            ["B2"] =
            [
                Session("e2e-run-1-android-1", "S3", "2026-10-21T09:58:05.000Z", "https://app-automate.browserstack.com/sessions/S3/video"),
                Session("e2e-run-1-android-2", "S2", "2026-10-21T09:58:07.000Z", Video),
            ],
            ["B1"] = [Session("e2e-run-1-android-2", "S1", "2026-10-20T08:00:00.000Z", "https://example.com/old")],
        };
    }

    private static JsonObject Build(string name, string id) => new() { ["automation_build"] = new JsonObject { ["name"] = name, ["hashed_id"] = id } };

    private static JsonObject Session(string name, string id, string? createdAt, string? video) => new()
    {
        ["automation_session"] = new JsonObject { ["name"] = name, ["hashed_id"] = id, ["created_at"] = createdAt, ["video_url"] = video },
    };

    private JsonObject SessionBody(string build, int index) => (JsonObject)_api.Sessions[build][index]["automation_session"]!;

    private static ProviderRecordContext Context(IReadOnlyDictionary<string, string?>? env = null) =>
        new() { RunId = "run-1", TargetName = "android", AttemptId = "attempt-1", Env = env ?? Env };

    private Task<ProviderRecording?> Record(DeviceLease? lease = null, ProviderRecordContext? context = null, CancellationToken cancellationToken = default) =>
        new BrowserStackDevices(Options, _ => new FakeLeases(), _api.Client()).RecordAsync(lease ?? Lease(), context ?? Context(), cancellationToken);

    private static async Task<ProviderRecordingResult> StopAsync(ProviderRecording recording) =>
        await recording.StopAsync(new ProviderRecordingStopContext { Dir = Path.GetTempPath() }, CancellationToken.None);

    private IEnumerable<string> Paths() => _api.Calls.Select(call => $"{call.Url.AbsolutePath}?{call.Url.Query.TrimStart('?')}");

    [Fact]
    public async Task Finds_the_newest_build_and_the_slots_session_by_name_and_starts_the_video_when_the_session_did()
    {
        var recording = (await Record())!;

        Assert.Equal(DateTimeOffset.Parse("2026-10-21T09:58:07.000Z", System.Globalization.CultureInfo.InvariantCulture), recording.StartedAt);
        var result = await StopAsync(recording);
        Assert.Equal((Video, "video/mp4"), (result.Url, result.MediaType));
        Assert.Equal(
            [
                "/app-automate/builds.json?limit=50&offset=0",
                "/app-automate/builds/B2/sessions.json?limit=50&offset=0",
                "/app-automate/builds.json?limit=50&offset=0",
                "/app-automate/builds/B2/sessions.json?limit=50&offset=0",
            ],
            Paths());
        Assert.Equal([$"Basic {Convert.ToBase64String("ada:bs-key"u8.ToArray())}"], _api.Calls.Select(call => call.Authorization).Distinct());
    }

    [Fact]
    public async Task Finds_the_session_in_an_older_build_of_the_same_name_as_another_project_of_the_run_holds_it()
    {
        _api.Sessions["B2"] = [];

        var recording = (await Record())!;

        Assert.Equal(DateTimeOffset.Parse("2026-10-20T08:00:00.000Z", System.Globalization.CultureInfo.InvariantCulture), recording.StartedAt);
        Assert.Equal(["/app-automate/builds.json", "/app-automate/builds/B2/sessions.json", "/app-automate/builds/B1/sessions.json"], _api.Calls.Select(call => call.Url.AbsolutePath));
    }

    [Fact]
    public async Task Reads_further_pages_of_builds_until_it_finds_the_one_named()
    {
        _api.Builds = [.. Enumerable.Range(0, 50).Select(i => Build($"other-{i}", $"X{i}")), .. _api.Builds];

        var recording = (await Record())!;

        Assert.Equal(DateTimeOffset.Parse("2026-10-21T09:58:07.000Z", System.Globalization.CultureInfo.InvariantCulture), recording.StartedAt);
        Assert.Equal(
            ["0", "50"],
            _api.Calls.Where(call => call.Url.AbsolutePath.EndsWith("/builds.json", StringComparison.Ordinal)).Select(call => System.Web.HttpUtility.ParseQueryString(call.Url.Query)["offset"]));
    }

    [Fact]
    public async Task Reads_the_video_when_the_session_ends_not_when_recording_starts()
    {
        SessionBody("B2", 1)["video_url"] = null;
        var recording = (await Record())!;
        SessionBody("B2", 1)["video_url"] = Video;

        var result = await StopAsync(recording);

        Assert.Equal((Video, "video/mp4"), (result.Url, result.MediaType));
    }

    [Fact]
    public async Task Names_the_session_that_reports_no_video()
    {
        SessionBody("B2", 1)["video_url"] = "not a url";
        var recording = (await Record())!;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => StopAsync(recording));

        Assert.Equal("BrowserStack session S2 (\"e2e-run-1-android-2\", build \"run-1\") reports no video URL", error.Message);
    }

    [Fact]
    public async Task Takes_the_moment_recording_was_asked_for_when_the_session_has_no_start_time()
    {
        SessionBody("B2", 1)["created_at"] = null;
        var before = DateTimeOffset.UtcNow;

        var recording = (await Record())!;

        Assert.True(recording.StartedAt >= before);
    }

    [Fact]
    public async Task Names_a_build_or_session_it_cannot_find()
    {
        var build = await Assert.ThrowsAsync<InvalidOperationException>(() => Record(Lease(build: "run-9")));
        Assert.Equal("BrowserStack has no build named \"run-9\"", build.Message);

        var session = await Assert.ThrowsAsync<InvalidOperationException>(() => Record(Lease(sessionName: "nope")));
        Assert.Equal("BrowserStack has no session named \"nope\" in build \"run-1\"", session.Message);
    }

    [Fact]
    public async Task Refuses_a_lease_without_the_build_and_session_name_acquire_put_on_it()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Record(new DeviceLease { Id = "lease-9", Client = new JsonObject { ["stateDir"] = "/x" } }));

        Assert.Equal("lease lease-9 carries no BrowserStack build and session name to find its recording by", error.Message);
    }

    [Fact]
    public async Task Reports_an_HTTP_failure_with_the_status_and_message_never_the_credentials()
    {
        _api.Overrides.Enqueue(() => FakeAppAutomate.Json(new JsonObject { ["message"] = "Unauthorized" }, HttpStatusCode.Unauthorized));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Record());

        Assert.Equal("BrowserStack build lookup for \"run-1\" failed: HTTP 401 (Unauthorized)", error.Message);
        Assert.DoesNotContain("bs-key", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reports_a_response_that_is_not_JSON_or_not_a_list()
    {
        _api.Overrides.Enqueue(() => new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("<html>") });
        var notJson = await Assert.ThrowsAsync<InvalidOperationException>(() => Record());
        Assert.Equal("BrowserStack build lookup for \"run-1\" failed: HTTP 502, not JSON", notJson.Message);

        _api.Overrides.Enqueue(() => FakeAppAutomate.Json(new JsonObject { ["builds"] = new JsonArray() }));
        var notList = await Assert.ThrowsAsync<InvalidOperationException>(() => Record());
        Assert.Equal("BrowserStack build lookup for \"run-1\" failed: no list in the response", notList.Message);
    }

    [Fact]
    public async Task Fails_before_any_request_without_credentials()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Record(context: Context(new Dictionary<string, string?>())));

        Assert.Contains("BROWSERSTACK_USERNAME and BROWSERSTACK_ACCESS_KEY are not set", error.Message, StringComparison.Ordinal);
        Assert.Empty(_api.Calls);
    }

    [Fact]
    public async Task Uses_BROWSERSTACK_API_ENDPOINT_when_set_and_refuses_one_that_is_not_http_s_or_carries_credentials()
    {
        await Record(context: Context(new Dictionary<string, string?>(Env) { ["BROWSERSTACK_API_ENDPOINT"] = "https://proxy.example.com/app-automate/" }));
        Assert.Equal("https://proxy.example.com/app-automate/builds.json?limit=50&offset=0", _api.Calls[0].Url.AbsoluteUri);

        var scheme = await Assert.ThrowsAsync<ConfigurationException>(() => Record(context: Context(new Dictionary<string, string?>(Env) { ["BROWSERSTACK_API_ENDPOINT"] = "ftp://x" })));
        Assert.Equal(("INVALID_CONFIG", "BROWSERSTACK_API_ENDPOINT is not an http(s) URL"), (scheme.Code, scheme.Message));

        var userInfo = await Assert.ThrowsAsync<ConfigurationException>(() => Record(context: Context(new Dictionary<string, string?>(Env) { ["BROWSERSTACK_API_ENDPOINT"] = "https://u:p@proxy.example.com" })));
        Assert.Equal("INVALID_CONFIG", userInfo.Code);
        Assert.Contains("must not carry a username or password", userInfo.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("u:p", userInfo.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stops_looking_once_the_run_is_cancelled()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Record(cancellationToken: cancelled.Token));

        Assert.Equal("BrowserStack build lookup for \"run-1\" cancelled", error.Message);
    }
}
