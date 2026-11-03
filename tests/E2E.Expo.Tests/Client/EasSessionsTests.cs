// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;
using E2E.Mobile;

namespace E2E.Expo.Tests.Client;

/// <summary>
/// <c>EasSessions</c> against Expo answers that are JSON but not the shape the sessions API documents: each fails with an
/// <c>Expo:</c> error naming the operation, never a <see cref="NullReferenceException"/> or a session state built from wrong types.
/// Ports <c>describe('easSessions()')</c> of <c>packages/eas/tests/unit/client.test.ts</c> (blob <c>b170551</c>, tester-army/e2e <c>79e2e13</c>).
/// </summary>
public sealed class EasSessionsTests
{
    private static readonly EasSessionParams Params = new() { AppId = "p1", Platform = MobilePlatform.Ios, Name = "e2e ios 1 of 1", Tags = [] };

    /// <summary>A client whose every request is answered with <paramref name="body"/> as JSON.</summary>
    private static EasSessions Answering(string body, int status = 200) =>
        new(new ExpoCredentials("expo-test", null), new HttpClient(new Answer(body, (HttpStatusCode)status)));

    [Fact]
    public async Task NamesAnAnswerThatIsNotAGraphQlObjectAsOneWithoutData()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Answering("null").StateAsync("s1", CancellationToken.None));
        Assert.Equal("Expo: HTTP 200, no data", error.Message);
    }

    [Fact]
    public async Task FallsBackToTheStatusForAGraphQlErrorThatIsNotAnObject()
    {
        var error = await Assert.ThrowsAsync<EasException>(() => Answering("""{ "errors": [null], "data": null }""").StateAsync("s1", CancellationToken.None));
        Assert.Equal("Expo: HTTP 200", error.Message);
    }

    [Fact]
    public async Task RejectsACreatedSessionWithoutItsApp()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Answering("""{ "data": { "deviceRunSession": { "createDeviceRunSession": { "id": "s1" } } } }""").CreateAsync(Params, CancellationToken.None));
        Assert.Equal("Expo: unexpected response to createDeviceRunSession", error.Message);
    }

    [Fact]
    public async Task RejectsASessionStateWithNoSessionOrADaemonAddressThatIsNotAString()
    {
        var none = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Answering("""{ "data": { "deviceRunSessions": { "byId": null } } }""").StateAsync("s1", CancellationToken.None));
        Assert.Equal("Expo: unexpected response to deviceRunSessions.byId", none.Message);
        var number = await Assert.ThrowsAsync<InvalidOperationException>(() => Answering("""
            { "data": { "deviceRunSessions": { "byId": {
              "status": "IN_PROGRESS",
              "turtleJobRun": { "status": "IN_PROGRESS" },
              "remoteConfig": { "agentDeviceRemoteSessionUrl": 42, "agentDeviceRemoteSessionToken": "daemon-token" }
            } } } }
            """).StateAsync("s1", CancellationToken.None));
        Assert.Equal("Expo: unexpected response to deviceRunSessions.byId", number.Message);
    }

    [Fact]
    public async Task ReadsADaemonAddressGraphQlAnswersAsNullAsNotReadyYet()
    {
        var state = await Answering("""
            { "data": { "deviceRunSessions": { "byId": {
              "status": "IN_PROGRESS",
              "turtleJobRun": { "status": "IN_PROGRESS" },
              "remoteConfig": { "agentDeviceRemoteSessionUrl": null, "agentDeviceRemoteSessionToken": null }
            } } } }
            """).StateAsync("s1", CancellationToken.None);
        Assert.Equal(new EasSessionState(EasPhase.Starting), state);
    }

    [Fact]
    public async Task RejectsAStopAnswerWithoutTheStoppedSession()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Answering("""{ "data": { "deviceRunSession": {} } }""").StopAsync("s1", CancellationToken.None));
        Assert.Equal("Expo: unexpected response to ensureDeviceRunSessionStopped", error.Message);
    }

    [Fact]
    public async Task RejectsAStopAnswerThatLeavesOutTheJobRatherThanSkipCancellingIt()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Answering("""{ "data": { "deviceRunSession": { "ensureDeviceRunSessionStopped": {} } } }""").StopAsync("s1", CancellationToken.None));
        Assert.Equal("Expo: unexpected response to ensureDeviceRunSessionStopped", error.Message);
    }

    /// <summary>.NET-only: upstream redacts at the report boundary; the port's provider hides its own token in what Expo answers.</summary>
    [Fact]
    public async Task HidesTheTokenInAnErrorExpoAnswersWith()
    {
        var graphql = await Assert.ThrowsAsync<EasException>(
            () => Answering("""{ "errors": [{ "message": "bad token expo-test" }] }""").StateAsync("s1", CancellationToken.None));
        Assert.Equal("Expo: bad token *****", graphql.Message);
        var page = await Assert.ThrowsAsync<InvalidOperationException>(() => Answering("Forbidden: expo-test", 403).StateAsync("s1", CancellationToken.None));
        Assert.Equal("Expo: HTTP 403, Forbidden: *****", page.Message);
    }

    private sealed class Answer(string body, HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
