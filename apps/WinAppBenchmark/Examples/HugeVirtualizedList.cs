// Copyright 2026 Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static WinAppBenchmark.App.Support;

namespace WinAppBenchmark.Examples;

/// <summary>
/// A very long virtualized list with no pagination: all 600 rows exist up front, but only the rows
/// near the viewport are materialized, so off-screen rows never appear in the UI Automation tree. The
/// only signals of how much content exists are the scroll extent and the row numbering: the agent must
/// size a long jump toward the target instead of paging one screenful at a time.
/// </summary>
public sealed class HugeVirtualizedList : Component
{
    private const int TotalRows = 600;
    private const int TargetRow = 512;
    private const double RowHeight = 56;

    private static readonly IReadOnlyList<int> Rows = [.. Enumerable.Range(1, TotalRows)];

    /// <summary>Formats a row number with leading zeros so labels sort and read consistently ("Row 0007", "Row 0512").</summary>
    private static string RowLabel(int row) => $"Row {row:0000}";

    public override Element Render()
    {
        var (found, setFound) = UseState(false);

        if (found)
        {
            return VStack(Success($"Found {RowLabel(TargetRow)}")).Padding(24);
        }

        return Grid(
            columns: [GridSize.Star()],
            rows: [GridSize.Auto, GridSize.Star()],
            Hint($"All {TotalRows} rows are loaded. Scroll to {RowLabel(TargetRow)} and tap it.").Padding(16, 12),
            ScrollView(ItemsRepeater(Rows, row => row.ToString(), (row, _) =>
                    Button(TextBlock(RowLabel(row)), () =>
                    {
                        if (row == TargetRow)
                        {
                            setFound(true);
                        }
                    })
                    .AutomationId($"row-{row}")
                    .Height(RowHeight)
                    .HAlign(HorizontalAlignment.Stretch)
                    .CornerRadius(0)))
                .AutomationName("Huge list").AutomationId("huge-list")
                .Grid(row: 1));
    }
}
