// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace E2E.Internal;

/// <summary>
/// The file-system and process calls <see cref="FileLock"/> makes, as a seam for tests. Upstream's <c>file-lock.test.ts</c> replaces
/// <c>statSync</c>, <c>writeFileSync</c>, <c>rmSync</c>, <c>utimesSync</c>, and <c>process.kill</c> with <c>vi.mock</c> and <c>vi.spyOn</c>;
/// .NET cannot patch its file APIs, so a test passes a subclass instead.
/// </summary>
internal class FileLockFileSystem
{
    /// <summary>The calls a lock makes outside a test.</summary>
    public static FileLockFileSystem Default { get; } = new();

    /// <summary>When the file at <paramref name="path"/> was last written (upstream's <c>statSync</c>); null when it does not exist.</summary>
    public virtual DateTimeOffset? LastWrite(string path) =>
        File.Exists(path) ? new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero) : null;

    /// <summary>Writes the holder to the new lock file (upstream's <c>writeFileSync</c>).</summary>
    public virtual void WriteToken(Stream stream, byte[] token)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(token);
        stream.Write(token);
    }

    /// <summary>Removes the file at <paramref name="path"/> (upstream's <c>rmSync</c>).</summary>
    public virtual void Delete(string path) => File.Delete(path);

    /// <summary>Marks the file as written at <paramref name="utc"/> (upstream's <c>utimesSync</c>).</summary>
    public virtual void SetLastWriteTimeUtc(string path, DateTime utc) => File.SetLastWriteTimeUtc(path, utc);

    /// <summary>True while <paramref name="pid"/> exists, whoever owns it (upstream's <c>running</c>, which signals the pid with <c>process.kill</c>).</summary>
    public virtual bool IsRunning(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // The process exists but is not ours to inspect.
            return true;
        }
    }
}
