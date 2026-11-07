// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentScrollPages;

/// <summary>How a swipe turns the page: now, late, or through a thinner page first.</summary>
internal enum Turning
{
    Now,
    Late,
    DrawnLate,
    Never,
}

/// <summary>What one scroll to a text did.</summary>
/// <param name="CapturesPerPage">Engine observations between each swipe and the next one, the last one up to the end of the scroll.</param>
/// <param name="Swipes">How many swipes the scroll made.</param>
/// <param name="Error">The code the scroll failed with, if it did.</param>
/// <param name="Found">What the list read when the scroll ended.</param>
internal sealed record Paged(IReadOnlyList<int> CapturesPerPage, int Swipes, string? Error, string? Found);

/// <summary>What a row reads: "Row 7", or a half-hourly slot ("9:00", "9:30") for a timetable whose rows differ by a clock-like value alone.</summary>
internal delegate string RowText(int index);

/// <summary>The scene and the helpers upstream's <c>agent-scroll-pages.test.ts</c> keeps at the top of its file.</summary>
internal static class ScrollPagesKit
{
    private const int PerPage = 3;

    public static readonly RowText Numbered = index => "Row " + index.ToString(CultureInfo.InvariantCulture);

    public static readonly RowText SlotAt = index =>
    {
        var minutes = 9 * 60 + index * 30;
        return (minutes / 60).ToString(CultureInfo.InvariantCulture) + ":" + (minutes % 60).ToString("00", CultureInfo.InvariantCulture);
    };

    // A clock that reads a minute later on every capture, as one ticking on the screen does.
    private static string ClockText(int minute) => "12:" + (minute % 60).ToString("00", CultureInfo.InvariantCulture);

    private static void RowsOf(DocumentElement list, int page, int count, RowText textOf)
    {
        list.Children.Clear();
        for (var at = 0; at < count; at++)
        {
            list.Children.Add(new DocumentElement
            {
                Role = "listitem",
                Text = textOf(page * PerPage + at),
                Rect = new BoundingBox(0, 20 + at * 40, 100, 40),
            });
        }
    }

    public static async Task<Paged> ScrollToAsync(string text, Turning turning, bool withClock = false, RowText? textOf = null, bool withCounter = false)
    {
        textOf ??= Numbered;
        var gate = new object();
        var observations = 0;
        var page = 0;
        var atSwipe = new List<int>();
        var list = new DocumentElement { Role = "list", Name = "Ledger" };
        var clock = new DocumentElement { Role = "status", Name = "Clock", Text = ClockText(0), Rect = new BoundingBox(0, 0, 100, 20) };
        var counter = new DocumentElement { Role = "status", Name = "Inbox", Text = "0 unread", Rect = new BoundingBox(0, 0, 100, 20) };
        RowsOf(list, 0, PerPage, textOf);

        void Turn(int to, int count = PerPage)
        {
            page = to;
            RowsOf(list, to, count, textOf);
        }

        void After(TimeSpan delay, Action turn) => _ = Task.Run(async () =>
        {
            await Task.Delay(delay).ConfigureAwait(false);
            lock (gate)
            {
                turn();
            }
        });

        var world = new DocumentWorld().Map("/", document =>
        {
            if (withClock)
            {
                document.Roots.Add(clock);
            }

            if (withCounter)
            {
                document.Roots.Add(counter);
            }

            document.Roots.Add(list);
            document.OnScroll = _ =>
            {
                lock (gate)
                {
                    atSwipe.Add(observations);
                    var next = turning == Turning.Never ? page : page + 1;
                    switch (turning)
                    {
                        case Turning.Now:
                            Turn(next);
                            break;
                        case Turning.Late:
                            After(TimeSpan.FromMilliseconds(40), () => Turn(page + 1));
                            break;
                        case Turning.DrawnLate:
                            Turn(next, 1);
                            After(TimeSpan.FromMilliseconds(60), () => Turn(next));
                            break;
                    }
                }
            };
        });
        var engine = new ObservedDocumentEngine(world, gate, () =>
        {
            observations++;
            clock.Text = ClockText(observations);
            counter.Text = observations.ToString(CultureInfo.InvariantCulture) + " unread";
        });

        string? error = null;
        string? found = null;
        List<int> capturesPerPage = [];
        var executor = new TestExecutor("scroll-pages", async (context, _) =>
        {
            await context.ObserveAsync();
            try
            {
                await context.Actions.ScrollUntilAsync(text, ScrollDirection.Down);
                lock (gate)
                {
                    found = string.Join(", ", list.Children.Select(row => row.Text));
                }
            }
            catch (TestException exception)
            {
                error = exception.Code;
            }

            lock (gate)
            {
                int[] ends = [.. atSwipe.Skip(1), observations];
                capturesPerPage = [.. ends.Select((end, index) => end - atSwipe[index])];
            }

            return Passed("scrolled");
        });

        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = engine,
            AgentExecutor = executor,
            BaseUrl = "https://ledger.test",
            CacheEnabled = false,
            TestTitle = "reaches the row",
            StepTimeout = TimeSpan.FromSeconds(60),
            TestTimeout = TimeSpan.FromSeconds(120),
        });
        await session.App.OpenAsync("/");
        await session.Agent.ActAsync("reach the row");
        session.Complete();
        lock (gate)
        {
            return new Paged(capturesPerPage, atSwipe.Count, error, found);
        }
    }
}
