// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Desktop.NUnit;

namespace DesktopCalculator;

/// <summary>
/// Drives the Calculator app that ships with macOS through <c>DesktopEngine</c>. The tests need a Mac with Accessibility permission granted to the
/// process that starts them (your terminal or IDE), and are skipped on any other OS. They are the sample for the desktop engine: no upstream test
/// covers them. The agent tests are in the <c>RealModel</c> category and need a GitHub Copilot login.
/// </summary>
public abstract class CalculatorTest : DesktopTest
{
    /// <summary>Skips the whole fixture before any test starts a session: the engine refuses to start anywhere but macOS.</summary>
    [OneTimeSetUp]
    public void RequireMacOS()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Assert.Ignore("The desktop engine drives macOS apps only.");
        }
    }

    [SetUp]
    public Task OpenCalculatorAsync() => App.OpenAsync();
}
