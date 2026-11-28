// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.XUnit.V3;

/// <summary>
/// Focuses the test or the fixture (upstream's <c>test.only</c> and <c>describe.only</c>). When any selected test is focused, the run executes the
/// focused tests, those of a focused fixture and of the fixtures nested in it included, and reports every other test as skipped with the cause
/// <c>filtered</c>. A focused test in a CI run is refused with <c>ONLY_IN_CI</c>, as upstream refuses <c>.only</c> there.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class OnlyAttribute : Attribute { }
