// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.RegularExpressions;

namespace E2E.GitHub;

/// <summary>
/// What GitHub Actions tells a job about itself (upstream's <c>ActionsContext</c>, <c>actions.ts</c>), read from the environment the runner sets: the
/// repository, the pull request the job runs for, the commit to link sources at, the token, and where the job summary is written.
/// </summary>
/// <param name="Repository"><c>owner/repo</c>.</param>
/// <param name="ServerUrl"><c>https://github.com</c>, or the GitHub Enterprise Server host.</param>
/// <param name="ApiUrl"><c>https://api.github.com</c>, or the Enterprise Server API root.</param>
/// <param name="RunUrl">The workflow run page, where uploaded artifacts live.</param>
/// <param name="EventName">The event that started the job.</param>
/// <param name="Workflow">The workflow's name.</param>
/// <param name="Job">The job's id.</param>
/// <param name="Sha">
/// The commit sources are linked at: the pull request's head when the event payload names it, the checked-out SHA when the event is not about a pull
/// request, and null on an <c>issue_comment</c>, whose SHA is the default branch and would link every source line to the wrong revision.
/// </param>
/// <param name="PullRequest">The pull request's number.</param>
/// <param name="Token"><c>GITHUB_TOKEN</c>, or <c>GH_TOKEN</c> the way the <c>gh</c> CLI reads it.</param>
/// <param name="StepSummaryPath">The file <c>GITHUB_STEP_SUMMARY</c> names, when the runner provides one.</param>
/// <param name="Workspace"><c>GITHUB_WORKSPACE</c>, the checkout root the repository's paths are relative to.</param>
internal sealed record ActionsContext(
    string Repository,
    string ServerUrl,
    string ApiUrl,
    string RunUrl,
    string EventName,
    string Workflow,
    string Job,
    string? Sha,
    int? PullRequest,
    string? Token,
    string? StepSummaryPath,
    string? Workspace);

/// <summary>What <see cref="Actions.DetectActionsAsync"/> touches outside itself, so tests run it against fakes (upstream's <c>ActionsDeps</c>).</summary>
/// <param name="Env">Reads an environment variable; null when it is unset.</param>
/// <param name="ReadFile">Reads a text file.</param>
internal record ActionsDeps(Func<string, string?> Env, Func<string, CancellationToken, Task<string>> ReadFile);

/// <summary>Upstream's <c>actions.ts</c>.</summary>
/// <remarks>
/// The pull request number comes from the event payload first, since <c>pull_request</c>, <c>pull_request_target</c>, and a PR's
/// <c>issue_comment</c> all carry it there, and from <c>GITHUB_REF</c> (<c>refs/pull/N/merge</c>) when the payload has none. Nothing here is read
/// until the run has finished.
/// </remarks>
internal static partial class Actions
{
    /// <summary>What a payload about a pull request says: its number, and its head when the event carries one.</summary>
    private sealed record PullRequestEvent(int? Number, string? HeadSha);

    [GeneratedRegex(@"\Arefs/pull/(\d+)/", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex PullRef();

    /// <summary>A variable's trimmed value; unset and blank both read as null.</summary>
    private static string? EnvValue(Func<string, string?> env, string name)
    {
        var raw = env(name);
        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
    }

    private static int? PullRequestNumber(JsonElement value) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number > 0 ? number : null;

    private static JsonElement? Property(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : null;

    /// <summary>Reads the pull request out of the webhook payload, or null when the payload is missing, unreadable, or not about a pull request.</summary>
    private static async Task<PullRequestEvent?> ReadEventAsync(string? path, ActionsDeps deps, CancellationToken cancellationToken)
    {
        if (path is null)
        {
            return null;
        }

        JsonDocument payload;
        try
        {
            payload = JsonDocument.Parse(await deps.ReadFile(path, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }

        using (payload)
        {
            var root = payload.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (Property(root, "pull_request") is { ValueKind: JsonValueKind.Object } pull)
            {
                var head = Property(pull, "head");
                return new PullRequestEvent(
                    Property(pull, "number") is { } number ? PullRequestNumber(number) : null,
                    head is { } h && Property(h, "sha") is { ValueKind: JsonValueKind.String } sha ? sha.GetString() : null);
            }

            // An `issue_comment` on a pull request: the issue carries a `pull_request` link but not the head commit, and GITHUB_SHA is the default branch here.
            if (Property(root, "issue") is { ValueKind: JsonValueKind.Object } issue && Property(issue, "pull_request") is { ValueKind: JsonValueKind.Object })
            {
                return new PullRequestEvent(Property(issue, "number") is { } number ? PullRequestNumber(number) : null, null);
            }

            return null;
        }
    }

    /// <summary>The number in <c>refs/pull/N/merge</c> or <c>refs/pull/N/head</c>.</summary>
    private static int? PullFromRef(string? reference)
    {
        var match = reference is null ? null : PullRef().Match(reference);
        return match is { Success: true } && int.TryParse(match.Groups[1].Value, out var number) && number > 0 ? number : null;
    }

    /// <summary>
    /// The Actions context of the current job, or null outside GitHub Actions (upstream's <c>detectActions</c>). On Actions the runner always sets the
    /// repository, run id, event name, workflow, and job; a job missing one is broken, not "not Actions", and says so.
    /// </summary>
    public static async Task<ActionsContext?> DetectActionsAsync(ActionsDeps deps, CancellationToken cancellationToken = default)
    {
        string? Value(string name) => EnvValue(deps.Env, name);
        if (Value("GITHUB_ACTIONS") is null)
        {
            return null;
        }

        string Required(string name) => Value(name) ?? throw new InvalidOperationException($"GITHUB_ACTIONS is set but {name} is not");

        var repository = Required("GITHUB_REPOSITORY");
        var serverUrl = (Value("GITHUB_SERVER_URL") ?? "https://github.com").TrimEnd('/');
        var pullEvent = await ReadEventAsync(Value("GITHUB_EVENT_PATH"), deps, cancellationToken).ConfigureAwait(false);

        return new ActionsContext(
            repository,
            serverUrl,
            (Value("GITHUB_API_URL") ?? "https://api.github.com").TrimEnd('/'),
            $"{serverUrl}/{repository}/actions/runs/{Required("GITHUB_RUN_ID")}",
            Required("GITHUB_EVENT_NAME"),
            Required("GITHUB_WORKFLOW"),
            Required("GITHUB_JOB"),
            pullEvent is null ? Value("GITHUB_SHA") : pullEvent.HeadSha,
            pullEvent?.Number ?? PullFromRef(Value("GITHUB_REF")),
            Value("GITHUB_TOKEN") ?? Value("GH_TOKEN"),
            Value("GITHUB_STEP_SUMMARY"),
            Value("GITHUB_WORKSPACE"));
    }
}
