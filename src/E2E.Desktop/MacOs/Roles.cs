// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Desktop.MacOs;

/// <summary>
/// macOS accessibility roles in the engine's raw role vocabulary, which the node projection maps onto ARIA names. The vocabulary is
/// agent-desktop's (Apache-2.0), kept so the snapshots it was tested on still project.
/// </summary>
internal static class Roles
{
    /// <summary>The raw role of an element, from its AX role and subrole.</summary>
    public static string Role(string axRole, string? subrole)
    {
        switch (subrole)
        {
            // An NSTabView item is an AXRadioButton that says by its subrole that it is a tab.
            case "AXTabButton":
                return "tab";
            case "AXApplicationAlert":
                return "alert";
            case "AXApplicationAlertDialog":
                return "alertdialog";
            case "AXApplicationDialog" or "AXDialog" or "AXSystemDialog":
                return "dialog";
            case "AXApplicationLog":
                return "log";
            case "AXApplicationMarquee":
                return "marquee";
            case "AXApplicationStatus":
                return "status";
            case "AXApplicationTimer":
                return "timer";
            case "AXDocumentArticle":
                return "article";
            case "AXDocumentMath":
                return "math";
            case "AXDocumentNote":
                return "note";
            case "AXDocumentRegion":
                return "region";
            case "AXLandmarkBanner":
                return "banner";
            case "AXLandmarkComplementary":
                return "complementary";
            case "AXLandmarkContentInfo":
                return "contentinfo";
            case "AXLandmarkForm":
                return "form";
            case "AXLandmarkMain":
                return "main";
            case "AXLandmarkNavigation":
                return "navigation";
            case "AXLandmarkSearch":
                return "search";
            case "AXDefinition":
                return "definition";
            case "AXTerm":
                return "term";
            case "AXTabPanel":
                return "tabpanel";
            case "AXUserInterfaceTooltip":
                return "tooltip";
            case "AXToggleButton":
            {
                var primary = Base(axRole);
                return primary is "checkbox" or "switch" or "radiobutton" ? primary : "button";
            }

            case "AXOutlineRow":
                return "treeitem";
            case "AXTableRow":
                return "row";
            case "AXSecureTextField" or "AXSearchField":
                return "textfield";
            case "AXCloseButton"
            or "AXMinimizeButton"
            or "AXZoomButton"
            or "AXToolbarButton"
            or "AXFullScreenButton"
            or "AXSortButton":
                return "button";
            case "AXToggle" or "AXSwitch":
                return "switch";
            case "AXContentList" or "AXDefinitionList" or "AXDescriptionList":
                return "list";
        }

        return Base(axRole);
    }

    private static string Base(string axRole) =>
        axRole switch
        {
            "AXApplication" => "application",
            "AXButton" => "button",
            "AXMenuButton" => "menubutton",
            "AXTextField" or "AXTextArea" or "AXSearchField" or "AXSecureTextField" => "textfield",
            "AXCheckBox" => "checkbox",
            "AXSwitch" or "AXToggle" => "switch",
            "AXLink" => "link",
            "AXMenuItem" or "AXMenuBarItem" => "menuitem",
            "AXRadioButton" => "radiobutton",
            "AXTab" => "tab",
            "AXTabGroup" => "tablist",
            "AXSlider" => "slider",
            "AXValueIndicator" or "AXHandle" => "handle",
            "AXComboBox" or "AXPopUpButton" => "combobox",
            "AXOutlineRow" => "treeitem",
            "AXRow" => "row",
            "AXCell" => "cell",
            "AXColumn" => "column",
            "AXWindow" => "window",
            "AXSheet" => "sheet",
            "AXDialog" => "dialog",
            "AXGroup" or "AXGenericElement" or "AXSplitGroup" => "group",
            "AXRadioGroup" => "radiogroup",
            "AXToolbar" => "toolbar",
            "AXStaticText" => "statictext",
            "AXImage" => "image",
            "AXTable" => "table",
            "AXList" => "list",
            "AXOutline" => "outline",
            "AXScrollArea" => "scrollarea",
            "AXScrollBar" => "scrollbar",
            "AXSplitter" => "splitter",
            "AXSeparator" => "separator",
            "AXMenu" or "AXMenuBar" => "menu",
            "AXIncrementor" or "AXStepper" => "incrementor",
            "AXDisclosureTriangle" => "disclosure",
            "AXProgressIndicator" or "AXBusyIndicator" => "progressbar",
            "AXColorWell" => "colorwell",
            "AXWebArea" => "webarea",
            "AXBrowser" => "browser",
            "AXGrid" => "grid",
            "AXPopover" => "popover",
            "AXTimeField" => "timefield",
            "AXDateField" => "datefield",
            "AXHelpTag" => "helptag",
            "AXMatte" => "matte",
            "AXDrawer" => "drawer",
            "AXLayoutArea" or "AXLayoutItem" => "layoutitem",
            "AXLevelIndicator" => "levelindicator",
            "AXRelevanceIndicator" => "relevanceindicator",
            "AXDocument" => "document",
            "AXHeading" => "heading",
            "AXParagraph" => "paragraph",
            "AXStatus" => "status",
            "AXToolTip" => "tooltip",
            _ => "unknown",
        };

    /// <summary>The name macOS draws for a window's title-bar button, which has no title of its own.</summary>
    public static string? WindowControlName(string? subrole) =>
        subrole switch
        {
            "AXCloseButton" => "Close",
            "AXMinimizeButton" => "Minimize",
            "AXZoomButton" => "Zoom",
            "AXFullScreenButton" => "Full Screen",
            _ => null,
        };

    /// <summary>Roles that are controls, and so get a ref whatever actions they list.</summary>
    public static bool Interactive(string role) =>
        role
            is "button"
                or "cell"
                or "checkbox"
                or "colorwell"
                or "combobox"
                or "incrementor"
                or "link"
                or "listbox"
                or "menubutton"
                or "menuitem"
                or "option"
                or "radiobutton"
                or "slider"
                or "switch"
                or "tab"
                or "textfield"
                or "treeitem"
                or "disclosure"
                or "datefield"
                or "timefield";

    /// <summary>Roles whose checked state their value carries.</summary>
    public static bool Toggleable(string role) => role is "checkbox" or "switch" or "radiobutton";

    /// <summary>Roles with an expanded or collapsed state.</summary>
    public static bool Expandable(string role) => role is "combobox" or "menubutton" or "treeitem" or "disclosure";

    /// <summary>Roles whose value a write can set.</summary>
    public static bool BearsValue(string role) =>
        role
            is "textfield"
                or "combobox"
                or "slider"
                or "incrementor"
                or "checkbox"
                or "radiobutton"
                or "switch"
                or "colorwell"
                or "scrollbar"
                or "handle"
                or "datefield"
                or "timefield";

    /// <summary>Roles that take typed text.</summary>
    public static bool TakesText(string role) => role is "textfield" or "combobox" or "datefield" or "timefield";

    /// <summary>Roles that select one of their items.</summary>
    public static bool Collection(string role) =>
        role is "list" or "listbox" or "table" or "outline" or "browser" or "radiogroup" or "tablist";

    /// <summary>Roles whose text is their value.</summary>
    public static bool Text(string role) => role is "statictext" or "heading";

    /// <summary>Roles that take their name from the text inside them when they have none of their own: a table's row and cell, an outline's item, a list's option.</summary>
    public static bool NamedByContent(string role) => role is "row" or "cell" or "treeitem" or "option" or "listitem";

    /// <summary>
    /// Roles that never take an action, so no action names are read for them. An image, a text label, or a heading is not one: an
    /// app can make any of them a button of its own (<c>AXPress</c>), which needs a ref to be tapped.
    /// </summary>
    public static bool Inert(string role) =>
        role
            is "separator"
                or "splitter"
                or "layoutitem"
                or "matte"
                or "paragraph"
                or "progressbar"
                or "levelindicator"
                or "relevanceindicator"
                or "helptag"
                or "tooltip";

    /// <summary>Roles that can hold many children, read a page at a time.</summary>
    public static bool Large(string role) => role is "table" or "outline" or "list" or "browser" or "grid" or "column";
}
