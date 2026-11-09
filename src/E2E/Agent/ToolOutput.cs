// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;

namespace E2E;

/// <summary>The limits <see cref="ToolOutput.BoundToolOutput"/> cuts to (upstream's <c>limits</c> argument, <c>{ maxLines, maxBytes }</c>).</summary>
/// <param name="MaxLines">Lines the result keeps, notice included.</param>
/// <param name="MaxBytes">UTF-8 bytes the result keeps, notice included.</param>
internal readonly record struct ToolOutputLimits(int MaxLines, int MaxBytes);

/// <summary>A project tool's result after <see cref="ToolOutput.BoundToolOutput"/> (upstream's <c>BoundToolOutput</c> interface).</summary>
/// <param name="Text">The result, with the notice appended when it was cut.</param>
/// <param name="Truncated">Whether a limit cut the result.</param>
/// <param name="By">Which limit cut the result, <c>lines</c> or <c>bytes</c>; <see langword="null"/> when it fit.</param>
/// <param name="TotalLines">Lines of the result before the cut.</param>
/// <param name="TotalBytes">UTF-8 bytes of the result before the cut.</param>
internal readonly record struct BoundToolOutput(string Text, bool Truncated, string? By, int TotalLines, int TotalBytes);

/// <summary>
/// The bound on what a project tool may hand the model in one result (upstream's <c>agent/tool-output.ts</c>). A tool that returns a
/// whole log or API payload would otherwise put it in the transcript for every later turn of the step. Two limits apply, lines and
/// bytes, whichever is reached first; the cut never splits a line, and the notice says exactly how much was left out, so the model
/// can ask for a narrower result instead of guessing at what it did not see.
/// </summary>
internal static class ToolOutput
{
    /// <summary>Lines a tool result keeps (upstream's <c>MAX_TOOL_OUTPUT_LINES</c>).</summary>
    internal const int MaxToolOutputLines = 400;

    /// <summary>Bytes a tool result keeps; screens are bounded separately by the observation budget (upstream's <c>MAX_TOOL_OUTPUT_BYTES</c>).</summary>
    internal const int MaxToolOutputBytes = 16 * 1024;

    /// <summary>Bytes set aside for the notice, so the whole result stays inside the limits (upstream's <c>NOTICE_RESERVE_BYTES</c>).</summary>
    private const int NoticeReserveBytes = 256;

    /// <summary>Lines set aside for the notice (upstream's <c>NOTICE_RESERVE_LINES</c>).</summary>
    private const int NoticeReserveLines = 1;

    private const string LinesLimit = "lines";
    private const string BytesLimit = "bytes";

    /// <summary>
    /// Cuts a tool result to the limits, with a notice naming what is missing. The cut lands on a line boundary, except for a first
    /// line that alone exceeds the byte budget, which keeps its head. The result, notice included, fits the limits it advertises.
    /// </summary>
    internal static BoundToolOutput BoundToolOutput(string text, ToolOutputLimits? limits = null)
    {
        var (maxLines, maxBytes) = limits ?? new ToolOutputLimits(MaxToolOutputLines, MaxToolOutputBytes);
        var totalBytes = Encoding.UTF8.GetByteCount(text);
        var lines = text.Split('\n');
        var totalLines = lines.Length;
        if (totalLines <= maxLines && totalBytes <= maxBytes)
        {
            return new BoundToolOutput(text, false, null, totalLines, totalBytes);
        }

        var lineBudget = Math.Max(1, maxLines - NoticeReserveLines);
        var byteBudget = Math.Max(64, maxBytes - NoticeReserveBytes);
        List<string> kept = [];
        var bytes = 0;
        var by = LinesLimit;
        foreach (var line in lines)
        {
            if (kept.Count >= lineBudget)
            {
                break;
            }

            var lineBytes = Encoding.UTF8.GetByteCount(line) + (kept.Count == 0 ? 0 : 1);
            if (bytes + lineBytes > byteBudget)
            {
                by = BytesLimit;
                break;
            }

            kept.Add(line);
            bytes += lineBytes;
        }

        // A first line longer than the whole byte budget keeps nothing of the text; its head is better than an empty result, and the
        // notice still says so. A cut inside a character leaves replacement characters at the end, which are dropped.
        if (kept.Count == 0)
        {
            var head = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(text).AsSpan(0, byteBudget)).TrimEnd('�');
            kept.Add(head);
            bytes = Encoding.UTF8.GetByteCount(head);
        }

        var notice = string.Create(
            CultureInfo.InvariantCulture,
            $"[tool output truncated: showing {kept.Count} of {totalLines} lines, {FormatBytes(bytes)} of {FormatBytes(totalBytes)}; the {by} limit was reached. Ask for a narrower result if you need the rest.]");
        return new BoundToolOutput(string.Join('\n', kept) + "\n" + notice, true, by, totalLines, totalBytes);
    }

    private static string FormatBytes(int bytes) =>
        bytes < 1024 ? bytes.ToString(CultureInfo.InvariantCulture) + " B" : (bytes / 1024.0).ToString("F1", CultureInfo.InvariantCulture) + " KB";
}
