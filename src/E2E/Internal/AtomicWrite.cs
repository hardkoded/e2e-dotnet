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
    // A POSIX rename replaces the target in one step. Windows refuses a move over a file that another move is replacing
    // (ERROR_ACCESS_DENIED) or that a reader holds open (ERROR_SHARING_VIOLATION), and both pass in a moment, so a move is
    // retried this often, with a growing pause, before its failure is the writer's. A denial that lasts, such as a read-only
    // target, reports the same error after the same ~90 ms.
    private const int MoveAttempts = 10;

    private const int AccessDenied = unchecked((int)0x80070005);

    private const int SharingViolation = unchecked((int)0x80070020);

    private static readonly UTF8Encoding Utf8 = new(false);

    /// <summary>Writes <paramref name="content"/> to <paramref name="filePath"/> atomically; a failed write leaves no temporary file behind.</summary>
    public static async Task WriteFileAtomicAsync(string filePath, string content, CancellationToken cancellationToken = default)
    {
        var temporary = TemporaryPath(filePath);
        try
        {
            await File.WriteAllTextAsync(temporary, content, Utf8, cancellationToken).ConfigureAwait(false);
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    File.Move(temporary, filePath, overwrite: true);
                    break;
                }
                catch (Exception ex) when (RetriesMove(ex, attempt))
                {
                    await Task.Delay(attempt * 2, cancellationToken).ConfigureAwait(false);
                }
            }
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

    /// <summary>The synchronous twin (upstream's <c>writeFileAtomicSync</c>), for a store that is read and written from synchronous code.</summary>
    public static void WriteFileAtomic(string filePath, string content)
    {
        var temporary = TemporaryPath(filePath);
        try
        {
            File.WriteAllText(temporary, content, Utf8);
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    File.Move(temporary, filePath, overwrite: true);
                    break;
                }
                catch (Exception ex) when (RetriesMove(ex, attempt))
                {
                    Thread.Sleep(attempt * 2);
                }
            }
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

    private static string TemporaryPath(string filePath) => filePath + "." + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8)) + ".tmp";

    private static bool RetriesMove(Exception ex, int attempt) =>
        OperatingSystem.IsWindows() && attempt < MoveAttempts && ex is (IOException or UnauthorizedAccessException) && ex.HResult is AccessDenied or SharingViolation;
}
