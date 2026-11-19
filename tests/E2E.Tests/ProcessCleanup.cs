// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel;
using System.Diagnostics;

namespace E2E.Tests;

/// <summary>Stops the worker processes a test started, as upstream's tests kill theirs in <c>onTestFinished</c>.</summary>
internal static class ProcessCleanup
{
    /// <summary>Kills each process tree and waits for it to exit, so the files it held can be deleted; one that cannot be stopped does not keep the others running.</summary>
    public static void KillAll(IEnumerable<Process> children)
    {
        foreach (var child in children)
        {
            try
            {
                if (!child.HasExited)
                {
                    child.Kill(entireProcessTree: true);
                }

                child.WaitForExit(5_000);
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                System.Diagnostics.Debug.WriteLine("could not stop worker " + child.Id + ": " + ex.Message);
            }
            finally
            {
                child.Dispose();
            }
        }
    }
}
