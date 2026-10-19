// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Engine;

/// <summary>
/// One line of what the app did on its own, as an engine reports it to <see cref="EngineStartOptions.AppLog"/> (upstream's
/// <c>AppLogEntry</c>): console output, an exception nothing caught, a failed request. The harness redacts and bounds it.
/// </summary>
/// <param name="Source">Where the line came from.</param>
/// <param name="Level">How bad it was.</param>
/// <param name="Text">The line. A blank one is dropped.</param>
public sealed record AppLogEntry(AppLogSource Source, AppLogLevel Level, string Text);
