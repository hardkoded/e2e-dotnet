// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Desktop.MacOs;

/// <summary>Choosing an option: a pop-up's menu, and a list's or table's rows, matched by the name the snapshot gives them.</summary>
internal static partial class Actions
{
    /// <summary>
    /// How deep <see cref="RowLabel"/> reads into a row for its text: as deep as a snapshot may read (<c>desktop({ maxDepth })</c>
    /// allows up to 50), so a row a locator names by its whole text is selected by that text too. A row deeper than this has a label it
    /// cannot read whole, which counts as a read the app did not answer: never a match, and never proof the option is missing.
    /// </summary>
    internal const int RowTextDepth = 50;

    /// <summary>How long each way of closing a menu nothing was chosen from waits for it, past the action's deadline too: an open menu covers the window.</summary>
    internal static readonly TimeSpan MenuCloseWait = TimeSpan.FromMilliseconds(500);

    /// <summary>How long a menu that refused <c>AXCancel</c> is watched before Escape: the refusal may still have closed it.</summary>
    internal static readonly TimeSpan MenuCloseGlance = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// The first of <paramref name="options"/> (the items of one title) that is enabled, waited for within the action's budget, as
    /// <see cref="Enabled"/> waits for one control; refused, naming the option, when none is enabled in time.
    /// </summary>
    internal static Element EnabledOf(IReadOnlyList<Element> options, Instant deadline, string label)
    {
        var refuseAt = deadline.CheckedSub(s_answerMargin) ?? deadline;
        while (true)
        {
            // An option the menu dropped since the scan is skipped: a live one of the title may still be enabled. With every one
            // gone, the menu changed under the select.
            var live = 0;
            foreach (var option in options)
            {
                var read = option.Attribute("AXEnabled");
                if (read.IsOk && read.Value!.Boolean == false)
                {
                    live++;
                }
                else if (!read.IsOk && read.Error == AxError.InvalidElement)
                {
                    // Skipped.
                }
                else if (!read.IsOk && read.Error is AxError.CannotComplete or AxError.ApiDisabled)
                {
                    throw Unread(read.Error, "select");
                }
                else
                {
                    return option;
                }
            }

            if (live == 0)
            {
                throw Unread(AxError.InvalidElement, "select");
            }

            if (Instant.Now + s_poll >= refuseAt)
            {
                throw Failure.Refused(Codes.ActionFailed, $"the option \"{label}\" is disabled");
            }

            Go("select");
            Thread.Sleep(s_poll);
        }
    }

    /// <summary>
    /// The name a list's row shows, as the snapshot names it: its own title or description (a text item's value), else, with none,
    /// every text inside it in reading order, joined by spaces.
    /// </summary>
    internal static AxResult<string?> RowLabel(Element element)
    {
        var own = OwnLabel(element);
        if (!own.IsOk)
        {
            return own;
        }

        if (own.Value is { } label)
        {
            return AxResult<string?>.Ok(label);
        }

        var texts = new List<string>();
        var read = RowTexts(element, 0, texts);
        return read != AxError.Success
            ? AxResult<string?>.Err(read)
            : AxResult<string?>.Ok(Snapshot.ContentName(texts));
    }

    /// <summary>
    /// An element's own name, as the snapshot gives it: its title, else the text of the element that labels it
    /// (<c>AXTitleUIElement</c>), else its description, else, for a text, its value.
    /// </summary>
    internal static AxResult<string?> OwnLabel(Element element)
    {
        var read = element.Attributes(["AXRole", "AXTitle", "AXDescription", "AXValue", "AXTitleUIElement"]);
        if (!read.IsOk)
        {
            return read.Error == AxError.CannotComplete
                ? AxResult<string?>.Err(AxError.CannotComplete)
                : AxResult<string?>.Ok(null);
        }

        var values = read.Value!;
        string? Text(int index) => values[index]?.Text is { } text && !string.IsNullOrWhiteSpace(text) ? text : null;
        var role = values[0]?.Text ?? string.Empty;
        string? labelled = null;
        if (values[4] is AxValue.ElementValue label)
        {
            var labelRead = Snapshot.LabelOf(label.Value);
            if (!labelRead.IsOk)
            {
                return AxResult<string?>.Err(labelRead.Error);
            }

            labelled = labelRead.Value;
        }

        return AxResult<string?>.Ok(
            Text(1) ?? labelled ?? Text(2) ?? (Roles.Text(Roles.Role(role, null)) ? Text(3) : null)
        );
    }

    /// <summary>The texts inside an element, in reading order, as the snapshot gathers a row's name from them. The result is an AX error, or <see cref="AxError.Success"/>.</summary>
    internal static int RowTexts(Element element, int depth, List<string> texts)
    {
        // Every child is read, as the snapshot reads them: a label cut short could match a prefix of the one asked for.
        AxResult<List<Element>> Children(Element parent)
        {
            var read = parent.Elements("AXChildren", Snapshot.MaxChildren);
            if (read.IsOk)
            {
                return read;
            }

            return read.Error == AxError.CannotComplete ? read : AxResult<List<Element>>.Ok([]);
        }

        AxResult<string> RoleOfChild(Element child)
        {
            var read = child.Attribute("AXRole");
            if (read.IsOk)
            {
                return AxResult<string>.Ok(Roles.Role(read.Value!.Text ?? string.Empty, null));
            }

            return read.Error == AxError.CannotComplete
                ? AxResult<string>.Err(AxError.CannotComplete)
                : AxResult<string>.Ok(Roles.Role(string.Empty, null));
        }

        return GatherTexts(element, depth, texts, Children, RoleOfChild, OwnLabel);
    }

    /// <summary>
    /// The texts under <paramref name="node"/>, gathered as the snapshot's walk gathers them for a name from content: every level is
    /// read, a text's own children before the text itself, so a row holding "Invoice" and an untitled heading over "Paid" is "Invoice
    /// Paid" here as it is in the snapshot. A scroll bar's parts are not read, as the snapshot reads none. A node deeper than
    /// <see cref="RowTextDepth"/> with children counts as a read the app did not answer.
    /// </summary>
    internal static int GatherTexts<T>(
        T node,
        int depth,
        List<string> texts,
        Func<T, AxResult<List<T>>> children,
        Func<T, AxResult<string>> role,
        Func<T, AxResult<string?>> label
    )
    {
        var below = children(node);
        if (!below.IsOk)
        {
            return below.Error;
        }

        if (below.Value!.Count > 0 && depth >= RowTextDepth)
        {
            return AxError.CannotComplete;
        }

        foreach (var child in below.Value!)
        {
            var kind = role(child);
            if (!kind.IsOk)
            {
                return kind.Error;
            }

            if (kind.Value != "scrollbar")
            {
                var gathered = GatherTexts(child, depth + 1, texts, children, role, label);
                if (gathered != AxError.Success)
                {
                    return gathered;
                }
            }

            if (Roles.Text(kind.Value!))
            {
                var text = label(child);
                if (!text.IsOk)
                {
                    return text.Error;
                }

                if (text.Value is { } value)
                {
                    texts.Add(value);
                }
            }
        }

        return AxError.Success;
    }

    /// <summary>The pop-up button's menu, if it is already open. An error when the app did not answer in time: a stall is no proof the menu is closed.</summary>
    internal static AxResult<Element?> CurrentMenu(Element button)
    {
        var read = button.Elements("AXChildren", 4);
        if (!read.IsOk)
        {
            return read.Error == AxError.CannotComplete
                ? AxResult<Element?>.Err(AxError.CannotComplete)
                : AxResult<Element?>.Ok(null);
        }

        foreach (var child in read.Value!)
        {
            var role = child.Attribute("AXRole");
            if (role.IsOk && role.Value!.Text == "AXMenu")
            {
                return AxResult<Element?>.Ok(child);
            }

            if (!role.IsOk && role.Error == AxError.CannotComplete)
            {
                return AxResult<Element?>.Err(AxError.CannotComplete);
            }
        }

        return AxResult<Element?>.Ok(null);
    }

    /// <summary>The open menu of a pop-up button, once it shows.</summary>
    internal static Element? OpenMenu(Element button, Instant deadline)
    {
        Element? menu = null;
        Until(deadline, () =>
        {
            var current = CurrentMenu(button);
            menu = current.IsOk ? current.Value : null;
            return menu is not null;
        });
        return menu;
    }

    /// <summary>
    /// Closes a menu nothing was chosen from, and answers whether it closed. It runs after the action's deadline too: each way of
    /// closing gets a wait of its own, so an <c>AXCancel</c> that used its wait still leaves Escape time to be seen.
    /// </summary>
    internal static bool CloseMenu(int pid, Element menu) =>
        CloseMenuWith(
            Cancelled,
            () => Windows.OpenMenus(pid) == 0,
            () => menu.Perform("AXCancel") == AxError.Success,
            () =>
            {
                // Escape goes to the process, which may have closed this menu and opened another: it is sent only while this menu is
                // still there, and a menu left open then makes the failure delivered. The read waits on the app, and the caller may
                // cancel meanwhile: it is checked again just before the key.
                if (menu.Attribute("AXRole").IsOk && !Cancelled())
                {
                    try
                    {
                        Input.Press(Input.ParseCombo("escape"), Route.Process(pid));
                    }
                    catch (Failure)
                    {
                        // The menu is checked for closed below.
                    }
                }
            }
        );

    /// <summary>
    /// <see cref="CloseMenu"/>'s steps, over <paramref name="stopped"/> (the caller cancelled), <paramref name="closed"/> (no menu is
    /// open), <paramref name="cancel"/> (sends <c>AXCancel</c>, and says whether the menu took it), and <paramref name="escape"/>
    /// (sends Escape).
    /// </summary>
    internal static bool CloseMenuWith(Func<bool> stopped, Func<bool> closed, Func<bool> cancel, Action escape)
    {
        // A cancelled action's caller has moved on, and may be working with this menu or another: nothing more is sent, and the menu
        // may be open.
        if (stopped())
        {
            return closed();
        }

        // A menu that answered the action with an error gets only a glance: an Escape sent once the menu closed would reach the
        // window under it (a sheet it cancels), so even then the driver looks before it sends one.
        var wait = cancel() ? MenuCloseWait : MenuCloseGlance;
        if (Until(Instant.After(wait), closed))
        {
            return true;
        }

        // A cancel that came while the menu was watched: an Escape now could close a menu the caller's next action opened.
        if (stopped())
        {
            return false;
        }

        escape();
        return Until(Instant.After(MenuCloseWait), closed);
    }

    /// <summary>
    /// The first <paramref name="limit"/> items <paramref name="matches"/> takes, and whether a read of one the app did not answer in
    /// time left an item unread: that item may be the one asked for. Each read waits on the app, and a list can hold thousands of items:
    /// the scan stops with the cut-off once the caller gave up or ran out of time.
    /// </summary>
    internal static (List<T> Found, bool Stalled) Scan<T>(
        IEnumerable<T> items,
        string what,
        int limit,
        Func<T, AxResult<bool>> matches
    )
    {
        var found = new List<T>();
        var stalled = false;
        foreach (var item in items)
        {
            Go(what);
            var read = matches(item);
            if (read.IsOk)
            {
                if (read.Value)
                {
                    found.Add(item);
                    if (found.Count >= limit)
                    {
                        break;
                    }
                }
            }
            else
            {
                stalled |= read.Error == AxError.CannotComplete;
            }
        }

        return (found, stalled);
    }

    /// <summary>
    /// A select's failure before anything was chosen, once its menu was closed or failed to close. A menu that stayed open changed the
    /// screen, and covers the window for the next action, so the failure is no longer "nothing sent".
    /// </summary>
    internal static Failure Unclosed(Failure failure, bool closed) =>
        closed ? failure : failure.Delivered().WithDetail("menu", "open");

    /// <summary>Runs <paramref name="work"/>, and maps a failure it throws (Rust's <c>map_err</c>).</summary>
    internal static T MapErr<T>(Func<T> work, Func<Failure, Failure> map)
    {
        try
        {
            return work();
        }
        catch (Failure failure)
        {
            throw map(failure);
        }
    }

    private static void Select(Entry entry, string label, Instant deadline)
    {
        var element = entry.Element;
        var (axRole, role) = RoleOf(element, "select");

        // A pull-down keeps its own title, so a choice cannot be read back from it: tap it and the menu item instead.
        if (axRole == "AXMenuButton")
        {
            throw Failure.Refused(
                Codes.ActionNotSupported,
                "a pull-down menu shows no selected option; tap it, then tap the menu item"
            );
        }

        // A disabled pop-up ignores the press that would open its list.
        Enabled(element, deadline, "select");
        if (axRole == "AXPopUpButton")
        {
            // A menu the user or a tap already opened on this button is the list: a press would close it. With it open, the option
            // shown is chosen from it too, so the menu does not stay open over the window.
            var openRead = CurrentMenu(element);
            if (!openRead.IsOk)
            {
                throw Unread(openRead.Error, "read the list's menu");
            }

            var alreadyOpen = openRead.Value;
            if (alreadyOpen is null && ValueText(element) == label)
            {
                return;
            }

            Element menu;
            if (alreadyOpen is not null)
            {
                menu = alreadyOpen;
            }
            else
            {
                // A menu still closing (the last select's) would take the press.
                Until(deadline, () => Windows.OpenMenus(entry.Pid) == 0);
                Perform(element, "AXPress", "open the list");
                menu = OpenMenu(element, deadline)
                    ?? throw Failure.Unconfirmed(Codes.ActionFailed, "the list's menu did not open");
            }

            // Until the item is pressed nothing is chosen: a failure closes the menu again, and says when it stayed open, since an
            // open menu covers the window for the next action.
            Failure Unchosen(Failure failure) => Unclosed(failure, CloseMenu(entry.Pid, menu));
            var items = MapErr(() => All(menu, "AXChildren", "read the list's options"), Unchosen);

            // A title the app did not answer in time may be the option asked for: that is no proof it is missing.
            var (found, stalled) = MapErr(() => Scan(items, "read the list's options", int.MaxValue, candidate =>
                    {
                        var title = candidate.Attribute("AXTitle");
                        return title.IsOk
                            ? AxResult<bool>.Ok(title.Value!.Text == label)
                            : AxResult<bool>.Err(title.Error);
                    }), Unchosen);
            if (found.Count == 0)
            {
                throw Unchosen(
                    stalled
                        ? Unread(AxError.CannotComplete, "read the list's options")
                        : Failure.Refused(Codes.ElementNotFound, $"the list has no option \"{label}\"")
                );
            }

            // An enabled option of that title is the one; a disabled one that comes first must not hide it. A disabled option takes
            // the press and ignores it: every one of that title is waited for, and the select refused as disabled, not as missing,
            // only when none is enabled in time.
            var item = MapErr(() => EnabledOf(found, deadline, label), Unchosen);
            MapErr(
                () =>
                {
                    Perform(item, "AXPress", "choose the option");
                    return 0;
                },
                // Refused before the press (out of time, cancelled, the item gone): nothing was chosen.
                failure => failure.Delivery == Delivery.NotDelivered ? Unchosen(failure) : failure
            );
            if (Until(deadline, () => ValueText(element) == label))
            {
                // The menu fades out after the choice; the next action must not land on it.
                Until(deadline, () => Windows.OpenMenus(entry.Pid) == 0);
                return;
            }

            throw Failure.Unconfirmed(Codes.ActionFailed, $"the list does not show \"{label}\" after it was chosen");
        }

        // An editable combo box is refused before this (a fill sets it); one that takes no typed value shows its list only when
        // opened.
        if (axRole == "AXComboBox")
        {
            throw Failure.Refused(
                Codes.ActionNotSupported,
                "a combo box that takes no typed value cannot be selected by label yet; tap it, then tap the option"
            );
        }

        if (Roles.Collection(role))
        {
            var rows = All(element, "AXRows", "read the list's rows");
            if (rows.Count == 0)
            {
                rows = All(element, "AXChildren", "read the list's rows");
            }

            // A row the app did not answer in time may be the option asked for: that is no proof it is missing. Two rows that show
            // the label leave the choice open: neither is selected, as no handler of the wrong one may run.
            var (found, stalled) = Scan(rows, "read the list's rows", 2, candidate =>
            {
                var text = RowLabel(candidate);
                return text.IsOk ? AxResult<bool>.Ok(text.Value == label) : AxResult<bool>.Err(text.Error);
            });
            if (found.Count > 1)
            {
                throw Failure.Refused(
                    Codes.ActionFailed,
                    $"more than one option of the list is \"{label}\"; select it another way"
                );
            }

            // A row whose label could not be read may show the label too: one match is no proof it is the only one.
            if (stalled)
            {
                throw Unread(AxError.CannotComplete, "read the list's rows");
            }

            if (found.Count == 0)
            {
                throw Failure.Refused(Codes.ElementNotFound, $"the list has no option \"{label}\"");
            }

            var row = found[0];
            var (_, rowRole) = RoleOf(row, "select");
            if (rowRole is "radiobutton" or "tab")
            {
                // An app can refuse the choice (an NSTabViewDelegate's shouldSelect) while the press itself succeeds: the option must
                // show it is the selected one.
                bool Selected() => row.Boolean("AXSelected") == true || ToggleState(row) == 1;
                if (!Selected())
                {
                    // A disabled option takes the press and ignores it.
                    Enabled(row, deadline, "select");
                    Perform(row, "AXPress", "choose the option");
                    if (!Until(deadline, Selected))
                    {
                        throw Failure.Unconfirmed(Codes.ActionFailed, $"\"{label}\" did not become selected");
                    }
                }

                return;
            }

            if (ChooseRow(element, row, deadline, $"\"{label}\" did not become selected"))
            {
                return;
            }

            throw Failure.Refused(Codes.ActionNotSupported, "the list takes no selection through accessibility");
        }

        throw Failure.Refused(Codes.ActionNotSupported, $"a {role} has no options to select");
    }

    /// <summary>
    /// Selects <paramref name="row"/>, through <paramref name="list"/>'s <c>AXSelectedRows</c> when the list takes it, else through
    /// the row's own <c>AXSelected</c>, and waits for the row to show it is selected (<paramref name="unselected"/> when it does not).
    /// False when neither can be written: nothing was sent.
    /// </summary>
    internal static bool ChooseRow(Element? list, Element row, Instant deadline, string unselected)
    {
        var byRows = list is not null && Settable(list, "AXSelectedRows", "select");
        if (!byRows && !Settable(row, "AXSelected", "select"))
        {
            return false;
        }

        // The reads before can take long on a slow app; the caller may have given up since.
        Go("select");
        var written = byRows ? list!.SetElements("AXSelectedRows", [row]) : row.SetBool("AXSelected", true);
        if (written != AxError.Success)
        {
            throw WriteFailed(written, "select the row");
        }

        return Until(deadline, () => row.Boolean("AXSelected") == true)
            ? true
            : throw Failure.Unconfirmed(Codes.ActionFailed, unselected);
    }
}
