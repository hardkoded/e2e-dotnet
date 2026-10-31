// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Internal;
using PolicyDecision = E2E.Reports.PolicyDecision;

namespace E2E;

/// <summary>The narrow surface the upload policy needs from its step machinery, the same member the secret fill records through (upstream's <c>UploadPolicyHost</c>).</summary>
internal interface IUploadPolicyHost
{
    void RecordPolicy(string name, PolicyDecision decision, string? code = null);
}

/// <summary>The paths as the executor gave them, and where each resolved to on disk (upstream's <c>AuthorizedUpload</c>).</summary>
internal sealed record AuthorizedUpload(IReadOnlyList<string> Given, IReadOnlyList<string> Resolved);

/// <summary>
/// Which files the agent may attach to a file input (upstream <c>agent/upload-paths.ts</c>). The model names a path; the harness decides,
/// before the engine sees it. Paths are project-relative, like <c>Locator.SetInputFilesAsync</c>, and a file a test author would not
/// hand the app is refused: one outside the project root (through a symlink too), one that is hidden or sits under a hidden directory
/// (<c>.env</c>, <c>.git</c>, <c>.e2e</c>), one that does not exist or is not a regular file. A page can talk a model into naming any
/// path; it cannot talk the harness into reading it.
/// </summary>
internal static class UploadPaths
{
    /// <summary>
    /// Authorizes the paths of one upload against the project root, recording every refusal and the final allow as an <c>upload.path</c>
    /// policy decision. Malformed input and a path that names no regular file are <c>INVALID_ARGUMENT</c>, the ordinary failure a model
    /// reads and corrects; a path the policy refuses is <c>POLICY_DENIED</c>. Upstream takes <c>unknown</c>; a null list, or a null entry
    /// where the model gave something that is no string, is the typed form of its malformed input.
    /// </summary>
    public static AuthorizedUpload AuthorizeUploadPaths(IUploadPolicyHost host, IReadOnlyList<string?>? paths, string projectRoot)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (paths is null || paths.Count == 0 || paths.Any(string.IsNullOrWhiteSpace))
        {
            throw new TestException("INVALID_ARGUMENT", "upload requires one or more non-empty project-relative file paths");
        }

        if (paths.Count > Trace.MaxTraceUploadPaths)
        {
            throw new TestException("INVALID_ARGUMENT", $"upload takes at most {Trace.MaxTraceUploadPaths} files at once");
        }

        var resolved = new List<string>(paths.Count);
        foreach (var entry in paths)
        {
            resolved.Add(Authorize(host, entry!, projectRoot));
        }

        host.RecordPolicy("upload.path", PolicyDecision.Allowed);
        return new AuthorizedUpload([.. paths!], resolved);
    }

    private static string Authorize(IUploadPolicyHost host, string entry, string projectRoot)
    {
        string absolute;
        string? relative;
        try
        {
            absolute = Path.GetFullPath(entry, projectRoot);
            relative = Paths.RelativeToProjectRoot(projectRoot, absolute);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // A path the filesystem cannot even spell names no file.
            throw new TestException("INVALID_ARGUMENT", $"no file at {Quote(entry)} under the project root");
        }

        if (relative is null)
        {
            throw Deny(host, $"{Quote(entry)} is outside the project root; only files inside the project can be uploaded");
        }

        if (relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(segment => segment.StartsWith('.')))
        {
            throw Deny(host, $"{Quote(entry)} is a hidden file or sits under a hidden directory; those are never uploaded");
        }

        if (Directory.Exists(absolute))
        {
            throw new TestException("INVALID_ARGUMENT", $"{Quote(entry)} is not a regular file");
        }

        if (!File.Exists(absolute))
        {
            throw new TestException("INVALID_ARGUMENT", $"no file at {Quote(entry)} under the project root");
        }

        return absolute;
    }

    // The policy refusal: recorded on the step first, then thrown as the model-readable denial.
    private static AgentException Deny(IUploadPolicyHost host, string reason)
    {
        host.RecordPolicy("upload.path", PolicyDecision.Denied, "POLICY_DENIED");
        return new AgentException("POLICY_DENIED", reason, blocked: true);
    }

    // JSON.stringify of a string, as upstream's messages quote a path.
    private static string Quote(string entry) => JsonSerializer.Serialize(entry, QuoteOptions);

    // JSON.stringify leaves non-ASCII text and '&', '<', '>' as written, so the relaxed encoder is the one that matches it.
    private static readonly JsonSerializerOptions QuoteOptions = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
}
