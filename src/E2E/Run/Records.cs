// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>The failure a runtime skip followed, with the evidence its attempt captured (upstream's <c>FailureBeforeSkip</c>, <c>run/records.ts</c>).</summary>
internal sealed record FailureBeforeSkip(
    ReportError Error,
    FailureEvidence? Failure,
    IReadOnlyList<ReportArtifact> Artifacts
);

/// <summary>A video a session recorded, as its owner hands it on: a file still on disk, or a link a hosted service keeps. Upstream's <c>SessionVideo</c> (<c>run/records.ts</c>).</summary>
internal abstract record SessionVideo;

/// <summary>A video file still on disk.</summary>
/// <param name="Path">The absolute path of the file.</param>
/// <param name="StartedAt">When the recording began, when it is known.</param>
internal sealed record SessionVideoFile(string Path, DateTimeOffset? StartedAt = null) : SessionVideo;

/// <summary>A video a hosted service keeps.</summary>
/// <param name="Url">Where the service keeps it.</param>
/// <param name="MediaType">Its media type.</param>
/// <param name="StartedAt">When the recording began.</param>
internal sealed record SessionVideoLink(string Url, string MediaType, DateTimeOffset StartedAt) : SessionVideo;

/// <summary>Reads over a result's records (upstream's <c>run/records.ts</c>).</summary>
internal static class Records
{
    /// <summary>
    /// The failure a test's <c>Skip(...)</c> followed: the last attempt that failed before a skipped retry, or an error the skipped attempt
    /// kept beside its skip. A soft failure before the skip fails its own attempt. Engine cleanup diagnostics after the skip are not one.
    /// Upstream's <c>failureBeforeSkip</c>, without its <c>groupOf</c> argument, which only a serial member reads: the port has no serial
    /// groups.
    /// </summary>
    public static FailureBeforeSkip? FailureBeforeSkip(ReportResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Status != ResultStatus.Skipped || result.Skip?.Cause != "explicit")
        {
            return null;
        }

        foreach (var run in result.Attempts.Reverse())
        {
            var error = run.Error ?? run.SecondaryErrors.FirstOrDefault(secondary => secondary.Phase != "cleanup");
            if (error is not null)
            {
                return new FailureBeforeSkip(error, run.Failure, run.Artifacts);
            }
        }

        return null;
    }
}
