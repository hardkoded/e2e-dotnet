// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;

namespace E2E.NUnit;

/// <summary>
/// Marks every test of the fixture as expected to fail, with the reason <c>expected to fail</c> (upstream's <c>describe.fail(title, fn)</c>, the registration
/// form). It is <c>[Property("E2E.Fail", "")]</c> on the fixture: a reason goes in <c>[Property("E2E.Fail", reason)]</c> (upstream's <c>describe(title, { fail: 'reason' }, fn)</c>),
/// so this form takes none, as <c>describe.fail</c> takes no <c>fail</c> option. Like that option, it is refused on, inside, or around a <c>[Serial]</c> fixture
/// with a <c>COLLECTION_ERROR</c>. Class-level only, a .NET-only shape: the port has no collection registry, so the host's own discovery finds the attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class DescribeFailAttribute() : PropertyAttribute(NUnitSessionTestRunner.ExpectedFailureProperty, "");
