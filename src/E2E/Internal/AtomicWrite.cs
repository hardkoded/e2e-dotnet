// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text;

namespace E2E.Internal;

/// <summary>
/// Atomic file replacement (upstream's <c>writeFileAtomic</c>, <c>internal/atomic-write.ts</c>): the content goes to a sibling temporary
/// file, which is then moved over the target, so a crash or a concurrent writer cannot leave a torn file behind. The temporary name carries
/// random bytes rather than the process id and the clock: two writers racing on one path would otherwise pick the same name.
/// </summary>
internal static class AtomicWrite
{
    /// <summary>Writes <paramref name="content"/> to <paramref name="filePath"/> atomically; a failed write leaves no temporary file behind.</summary>
    public static async Task WriteFileAtomicAsync(string filePath, string content, CancellationToken cancellationToken = default)
    {
        var temporary = filePath + "." + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8)) + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, content, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            File.Move(temporary, filePath, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                // The write may have failed before the temporary file existed, and the first failure is the one to report.
            }

            throw;
        }
    }
}
