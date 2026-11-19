// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Runtime.InteropServices;

namespace E2E.Decision.Sdk.Gateway.VercelOidc;

/// <summary>.NET-only helper for the owner-only file modes <c>@vercel/oidc</c> sets through <c>fs</c>.</summary>
internal static class FileModes
{
    private const int UserRead = 0x100;
    private const int UserWrite = 0x80;
    private const int UserExecute = 0x40;
    private const int GroupRead = 0x20;
    private const int GroupWrite = 0x10;
    private const int GroupExecute = 0x8;

    /// <summary>
    /// Writes a file with mode 0600, or 0660 when <paramref name="groupAccess"/> is set, on platforms that have file modes.
    /// The content goes to a new file with a unique name, which is restricted before it receives the secret and then replaces the target.
    /// </summary>
    public static void WriteRestricted(string path, string content, bool groupAccess = false)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        string? stagingDir = null;
        try
        {
            var mode = UserRead | UserWrite | (groupAccess ? GroupRead | GroupWrite : 0);
#if NET
            // The file is created with its mode, so no other user can open it first.
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
            };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = (UnixFileMode)mode;
            }

            using (var stream = new FileStream(temp, options))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(content);
            }
#else
            // netstandard2.0 has no create-with-mode API. The file is staged in a new directory that is restricted to its owner
            // (mode 0700) before the file exists, so no other user can reach it while its own mode is still the default.
            stagingDir = temp + ".d";
            if (Directory.Exists(stagingDir))
            {
                throw new IOException("The staging directory already exists.");
            }

            Directory.CreateDirectory(stagingDir);
            SetMode(stagingDir, UserRead | UserWrite | UserExecute);
            temp = Path.Combine(stagingDir, "secret");
            // CreateNew fails if another user created the name first, so a planted file is rejected, never written to.
            // The handle stays open through the write, so the file cannot be swapped between the mode change and the write.
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write))
            {
                SetMode(temp, mode);
                using var writer = new StreamWriter(stream);
                writer.Write(content);
            }
#endif
#if NET
            File.Move(temp, path, overwrite: true);
#else
            if (File.Exists(path))
            {
                File.Replace(temp, path, null);
            }
            else
            {
                File.Move(temp, path);
            }
#endif
        }
        catch
        {
            try
            {
                File.Delete(temp);
            }
            catch (Exception exception)
                when (exception is IOException or UnauthorizedAccessException)
            {
                // The original failure is the one to report.
            }

            throw;
        }
        finally
        {
            if (stagingDir is not null)
            {
                try
                {
                    Directory.Delete(stagingDir, recursive: true);
                }
                catch (Exception exception)
                    when (exception is IOException or UnauthorizedAccessException)
                {
                    // Best effort: the staging directory is empty and private.
                }
            }
        }
    }

    /// <summary>Sets mode 0770 on a directory this code created.</summary>
    public static void RestrictDirectory(string path) =>
        SetMode(path, UserRead | UserWrite | UserExecute | GroupRead | GroupWrite | GroupExecute);

    /// <summary>
    /// Sets a Unix mode. Does nothing on Windows. Throws on a Unix runtime older than .NET 7, which has no <c>File.SetUnixFileMode</c>,
    /// so a secret is never written with the default mode.
    /// </summary>
    private static void SetMode(string path, int mode)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

#if NET
        File.SetUnixFileMode(path, (UnixFileMode)mode);
#else
        var modeType = typeof(File).Assembly.GetType("System.IO.UnixFileMode");
        var method = modeType is null
            ? null
            : typeof(File).GetMethod("SetUnixFileMode", [typeof(string), modeType]);
        if (method is null)
        {
            throw new PlatformNotSupportedException(
                "Cannot restrict file permissions on this runtime. Use .NET 7 or later."
            );
        }

        method.Invoke(null, [path, Enum.ToObject(modeType!, mode)]);
#endif
    }
}
