// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli.Tests.Helpers;

/// <summary>
/// A captured console whose lines end in <c>\n</c> on every host, as upstream's console does: the CLI writes each line through
/// <see cref="TextWriter.WriteLine()"/> on the writer it is given, which ends it with <see cref="Environment.NewLine"/> otherwise, and a
/// test compares the capture with text joined by <c>\n</c>.
/// </summary>
internal sealed class LineFeedWriter : StringWriter
{
    public LineFeedWriter()
    {
        NewLine = "\n";
    }
}
