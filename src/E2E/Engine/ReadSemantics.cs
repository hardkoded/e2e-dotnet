// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Engine;

// Upstream's in-page/read-semantics.ts, the part that reads one element on its own: role, accessible name, state,
// labels, DOM text, attributes and rect. It is a fragment of function-body declarations, not a function of its own, so
// the tree reader (PageScript.Collect) and the selector engines embed it first (upstream's e2e-label and e2e-shown embed
// readSemanticsFunction and call it in mode node and hidden: here LabelSelector calls labelsOf and ShownSelector calls
// HiddenMode); each embedding has its own memo caches. A helper here reads the element it is given
// and no walk state: it closes over nothing the walk owns, and takes what the walk keeps as arguments (domTextOf its
// text limit, attributesOf the test id attribute).
internal static class ReadSemantics
{
    public const string Prelude = """
          // Named as upstream's SKIP_TAGS in read-semantics.ts, which also lists next-route-announcer: Next.js App
          // Router's route announcer echoes the title on a hydration schedule of its own, which an observation must
          // not read as an alert.
          const skip = new Set(["SCRIPT", "STYLE", "NOSCRIPT", "TEMPLATE", "NEXT-ROUTE-ANNOUNCER"]);
          const closedRoots = globalThis[Symbol.for("e2e.closedShadowRoots")];
          const shadowOf = (el) => el.shadowRoot ?? closedRoots?.get(el) ?? null;
          const hasOwnName = (el) => ["aria-label", "aria-labelledby", "title"].some((attribute) => (el.getAttribute(attribute) || "").trim() !== "");
          // A shadow tree sits inside its host's sections, so the scope goes up through the hosts.
          const pageLevel = (el) => {
            for (let current = el; current; ) {
              if (current.closest("article, aside, main, nav, section, [role~=article], [role~=complementary], [role~=main], [role~=navigation], [role~=region]") !== null) return false;
              const root = current.getRootNode();
              current = root instanceof ShadowRoot ? root.host : null;
            }
            return true;
          };
          // Tags a contenteditable never makes an editing host: a drawn or embedded
          // surface, a void element, and a native control with a value of its own.
          // Named and shaped as upstream's OPAQUE_TAGS and NON_HOST_TAGS in read-semantics.ts.
          const OPAQUE_TAGS = ["svg", "math", "canvas", "video", "audio"];
          const NON_HOST_TAGS = [...OPAQUE_TAGS, "iframe", "img", "hr", "br", "wbr", "area", "a", "button", "input", "textarea", "select"];
          const isEditingHost = (el) => el instanceof HTMLElement && el.isContentEditable && NON_HOST_TAGS.indexOf(el.tagName.toLowerCase()) === -1
            && !(el.parentElement instanceof HTMLElement && el.parentElement.isContentEditable)
            // Port only: a document in designMode makes <html> editable, and the page is not a control.
            && el !== el.ownerDocument.documentElement;
          // The text selected inside a focused field: the slice between a field's
          // selection ends, or the document selection inside an editing host.
          // Null for a collapsed caret.
          const selectedTextOf = (el) => {
            if (el instanceof HTMLInputElement || el instanceof HTMLTextAreaElement) {
              const start = el.selectionStart;
              const end = el.selectionEnd;
              if (start === null || end === null || start === end) return null;
              return el.value.slice(Math.min(start, end), Math.max(start, end));
            }
            if (!isEditingHost(el)) return null;
            const selection = el.ownerDocument.getSelection();
            if (selection === null || selection.rangeCount === 0 || selection.isCollapsed || !el.contains(selection.getRangeAt(0).commonAncestorContainer)) return null;
            const text = selection.toString();
            return text === "" ? null : text;
          };
          const cut = (value, limit) => {
            const text = (value || "").replace(/\s+/g, " ").trim();
            return text.length > limit ? text.slice(0, limit) : text;
          };
          // Content a closed details folds away: anything under it outside its
          // summary. Every closed ancestor is asked, nested details included.
          const inClosedDetails = (el) => {
            for (let details = el.parentElement?.closest("details"); details; details = details.parentElement?.closest("details")) {
              if (!details.open) {
                const summary = details.querySelector(":scope > summary");
                if (!summary || !summary.contains(el)) return true;
              }
            }
            return false;
          };
          // A text node that lays out to a box a person can see.
          const visibleText = (node) => {
            const range = document.createRange();
            range.selectNode(node);
            const box = range.getBoundingClientRect();
            return box.width > 0 && box.height > 0;
          };
          // Render visibility, as Playwright reads it: display: none, a
          // visibility other than visible, and content a closed details folds
          // away. A display: contents element has no box; it is shown when a
          // child element or its own text is. Two parts are not in it, since
          // role queries and the agent read the same hidden state as the
          // accessibility tree: a box with no size, which an empty landmark
          // has, and aria-hidden, which the node carries apart from this
          // since it still paints.
          const hidden = (el, style = getComputedStyle(el)) => {
            if (style.display === "contents") {
              // A host paints its shadow tree, so that counts as its children too.
              const children = [...el.childNodes, ...(shadowOf(el)?.childNodes ?? [])];
              for (const child of children) {
                if (child.nodeType === 1 && !skip.has(child.tagName) && !hidden(child)) return false;
                if (child.nodeType === 3 && style.visibility === "visible" && visibleText(child)) return false;
              }
              return true;
            }
            return style.display === "none" || style.visibility !== "visible" || inClosedDetails(el);
          };
          const ariaHidden = (el) => (el.getAttribute("aria-hidden") || "").toLowerCase() === "true";
          // What stops the element and everything under it from rendering:
          // display: none, which no descendant can undo, and content-visibility:
          // hidden, which keeps the element's own box and renders nothing under
          // it. visibility is not in it, since a child may set visibility:
          // visible and paint again; nor is aria-hidden, which only the
          // accessibility tree drops.
          const hidesSubtree = (el, style) => style.display === "none" || style.contentVisibility === "hidden";
          // Port only: upstream has no paragraph role, so it needs no test for content inside a host.
          const inEditingHost = (el) => {
            if (el.parentElement?.closest("[contenteditable]") == null) return false;
            for (let parent = el.parentElement; parent !== null; parent = parent.parentElement) {
              if (isEditingHost(parent)) return true;
            }
            return false;
          };
          // Concrete WAI-ARIA roles, excluding abstract roles that cannot label an element.
          const ARIA_ROLES = [
            "alert", "alertdialog", "application", "article", "banner", "blockquote", "button", "caption",
            "cell", "checkbox", "code", "columnheader", "combobox", "complementary", "contentinfo", "definition",
            "deletion", "dialog", "directory", "document", "emphasis", "feed", "figure", "form",
            "generic", "grid", "gridcell", "group", "heading", "img", "insertion", "link",
            "list", "listbox", "listitem", "log", "main", "mark", "marquee", "math",
            "meter", "menu", "menubar", "menuitem", "menuitemcheckbox", "menuitemradio", "navigation", "none",
            "note", "option", "paragraph", "presentation", "progressbar", "radio", "radiogroup", "region",
            "row", "rowgroup", "rowheader", "scrollbar", "search", "searchbox", "separator", "slider",
            "spinbutton", "status", "strong", "subscript", "superscript", "switch", "tab", "table",
            "tablist", "tabpanel", "term", "textbox", "time", "timer", "toolbar", "tooltip",
            "tree", "treegrid", "treeitem",
          ];
          // ARIA uses the first recognized token, then native semantics if none is recognized.
          const explicitRole = (el) =>
            (el.getAttribute("role") ?? "").split(/[ \t\n\f\r]+/).find((role) => ARIA_ROLES.indexOf(role) !== -1) ?? null;
          const NAME_PROHIBITED_ROLES = [
            "caption", "code", "deletion", "emphasis", "generic", "insertion", "paragraph", "presentation", "strong",
            "subscript", "superscript",
          ];
          // Global ARIA attributes, each with the roles it does not count on, as Playwright lists them.
          const GLOBAL_ARIA_ATTRIBUTES = [
            ["aria-atomic", []],
            ["aria-busy", []],
            ["aria-controls", []],
            ["aria-current", []],
            ["aria-describedby", []],
            ["aria-details", []],
            ["aria-dropeffect", []],
            ["aria-flowto", []],
            ["aria-grabbed", []],
            ["aria-hidden", []],
            ["aria-keyshortcuts", []],
            ["aria-label", NAME_PROHIBITED_ROLES],
            ["aria-labelledby", NAME_PROHIBITED_ROLES],
            ["aria-live", []],
            ["aria-owns", []],
            ["aria-relevant", []],
            ["aria-roledescription", ["generic"]],
          ];
          // Playwright's implicit role for the tags whose role some global ARIA attributes do not count on.
          const NAME_PROHIBITED_TAG_ROLES = {
            caption: "caption", code: "code", del: "deletion", em: "emphasis", ins: "insertion", p: "paragraph",
            strong: "strong", sub: "subscript", sup: "superscript",
          };
          // True when the element carries a global ARIA attribute that counts on `role`.
          const hasGlobalAriaAttribute = (el, role) =>
            GLOBAL_ARIA_ATTRIBUTES.some(([name, prohibited]) =>
              (role === null || prohibited.indexOf(role) === -1) && el.hasAttribute(name));
          // True when `tabindex` parses as a number, as Playwright reads it.
          const hasTabIndex = (el) => !Number.isNaN(Number(String(el.getAttribute("tabindex"))));
          // Whether a person can focus the element, as Playwright decides presentational-role conflicts.
          const isFocusable = (el) => {
            const tag = el.tagName.toLowerCase();
            if (["button", "input", "select", "textarea", "option", "optgroup"].indexOf(tag) !== -1 && el.matches(":disabled")) {
              return false;
            }
            if (hasTabIndex(el) || ["button", "details", "select", "textarea"].indexOf(tag) !== -1) return true;
            if (tag === "a" || tag === "area") return el.hasAttribute("href");
            return tag === "input" && !el.hidden;
          };
          // WAI-ARIA presentational-role conflict resolution: `presentation` or `none` is ignored on a
          // focusable element or one with a global ARIA attribute, and the tag's role stands.
          const hasPresentationConflict = (el) =>
            isFocusable(el) || hasGlobalAriaAttribute(el, NAME_PROHIBITED_TAG_ROLES[el.tagName.toLowerCase()] ?? null);
          const roleOf = (el) => {
            const explicit = explicitRole(el);
            if (explicit !== null && !((explicit === "presentation" || explicit === "none") && hasPresentationConflict(el))) {
              // The vocabulary spells ARIA's `img` as `image`.
              return explicit === "img" ? "image" : explicit;
            }
            // Ahead of the tag: an editor's host is the control, whatever landmark or structure its tag would otherwise be.
            if (isEditingHost(el)) return "textbox";
            const tag = el.tagName;
            switch (tag) {
              case "A": return el.hasAttribute("href") ? "link" : null;
              case "BUTTON": return "button";
              case "SELECT": return el.multiple || el.size > 1 ? "listbox" : "combobox";
              case "TEXTAREA": return "textbox";
              case "IMG":
                return el.getAttribute("alt") === "" && !el.getAttribute("title") && !hasGlobalAriaAttribute(el, null) && !hasTabIndex(el)
                  ? "presentation"
                  : "image";
              // An inline icon is a picture whether or not anything names it, as
              // Playwright reads it; Chrome ignores an unnamed one.
              case "svg": return "image";
              case "NAV": return "navigation";
              case "MAIN": return "main";
              case "ASIDE": return "complementary";
              case "HEADER": return pageLevel(el) ? "banner" : null;
              case "FOOTER": return pageLevel(el) ? "contentinfo" : null;
              case "SECTION": return hasOwnName(el) ? "region" : null;
              case "FORM": return hasOwnName(el) ? "form" : null;
              case "ARTICLE": return "article";
              case "FIGURE": return "figure";
              case "HR": return "separator";
              case "PROGRESS": return "progressbar";
              case "METER": return "meter";
              case "MENU": case "UL": case "OL": return "list";
              case "FIELDSET": case "DETAILS": case "OPTGROUP": case "ADDRESS": return "group";
              case "OPTION": return "option";
              case "LI": return "listitem";
              case "TABLE": return "table";
              case "THEAD": case "TBODY": case "TFOOT": return "rowgroup";
              case "TR": return "row";
              case "TD": {
                const table = el.closest("table");
                const tableRole = table === null ? null : explicitRole(table);
                return tableRole === "grid" || tableRole === "treegrid" ? "gridcell" : "cell";
              }
              case "TH": {
                const scope = (el.getAttribute("scope") || "").toLowerCase();
                if (scope === "row" || scope === "rowgroup") return "rowheader";
                if (scope === "col" || scope === "colgroup") return "columnheader";
                const previous = el.previousElementSibling;
                const next = el.nextElementSibling;
                if (previous === null && next === null) {
                  const row = el.parentElement;
                  const table = row?.tagName === "TR" ? row.closest("table") : null;
                  return table !== null && table.rows.length <= 1 ? null : "columnheader";
                }
                if (previous?.tagName === "TH" && next?.tagName === "TH") return "columnheader";
                const hasDataNeighbor = [previous, next].some((cell) =>
                  cell?.tagName === "TD" && ((cell.textContent || "").trim() !== "" || cell.children.length > 0));
                return hasDataNeighbor ? "rowheader" : "columnheader";
              }
              case "DIALOG": return "dialog";
              case "OUTPUT": return "status";
              case "P": return inEditingHost(el) ? null : "paragraph";
              case "IFRAME": return "iframe";
              case "INPUT": {
                const type = (el.getAttribute("type") || "text").toLowerCase();
                if (type === "hidden") return null;
                if (el.list && ["text", "search", "tel", "url", "email"].includes(el.type)) return "combobox";
                if (type === "checkbox") return "checkbox";
                if (type === "radio") return "radio";
                if (["button", "submit", "reset", "image", "file"].includes(type)) return "button";
                if (type === "range") return "slider";
                if (type === "number") return "spinbutton";
                if (type === "search") return "searchbox";
                return "textbox";
              }
            }
            if (/^H[1-6]$/.test(tag)) return "heading";
            if (el.getAttribute("aria-live")) return "status";
            return null;
          };
          // The accessible name, by upstream's accname rules. A walk's
          // inReference is set inside an aria-labelledby traversal, where a
          // nested reference is not followed (2B); hiddenAllowed when that
          // traversal began at a hidden target, whose whole subtree counts (2A);
          // visited holds every element already read, so each contributes once.
          // accessible is set for an accessible name, which reads CSS generated
          // content and an embedded control's value, and unset for label text.
          const nameWalk = (visited, accessible) => ({ inReference: false, hiddenAllowed: false, visited: new Set(visited), accessible });
          // Icon-font glyphs (private-use code points) read as nothing a person could type; each becomes a space.
          const iconGlyphs = /\p{Co}/gu;
          const withoutGlyphs = (name) => {
            const spaced = name.replace(iconGlyphs, " ");
            return spaced === name ? name : spaced.replace(/\s+/g, " ").trim();
          };
          // A name of icon glyphs alone falls back to the element's title, else to no name.
          const reportedName = (el, name) => {
            if (name === null) return null;
            const shown = withoutGlyphs(name);
            if (shown !== "" || shown === name) return shown;
            const title = (el.getAttribute("title") || "").trim();
            return title === "" ? null : title;
          };
          // The text a content value contributes: its strings and attr() values,
          // or only the alternative text after a "/". Anything else contributes nothing.
          const contentTextOf = (el, value) => {
            const tokens = [];
            const token = /\s*("(?:[^"\\]|\\.)*"|'(?:[^'\\]|\\.)*'|[-\w]+\((?:[^()"']|"(?:[^"\\]|\\.)*"|'(?:[^'\\]|\\.)*')*\)|\/|[-\w]+)/gy;
            let match;
            let end = 0;
            while ((match = token.exec(value)) !== null) {
              tokens.push(match[1]);
              end = token.lastIndex;
            }
            if (value.slice(end).trim() !== "") return null;
            let text = "";
            for (const part of tokens.slice(tokens.lastIndexOf("/") + 1)) {
              const attribute = /^attr\(\s*([-\w]+)\s*\)$/.exec(part);
              if (attribute !== null) text += el.getAttribute(attribute[1]) ?? "";
              else if (part.startsWith('"') || part.startsWith("'")) {
                text += part.slice(1, -1).replace(/\\([0-9a-fA-F]{1,6})\s?|\\(.)/g, (_all, hex, char) =>
                  hex === undefined ? char : String.fromCodePoint(Number.parseInt(hex, 16)));
              } else return null;
            }
            return text;
          };
          // What an element's ::before or ::after adds to a name (accname 2F.ii), spaced like a block when it is not inline.
          const generatedContentOf = (el, pseudo) => {
            const style = getComputedStyle(el, pseudo);
            if (style.display === "none" || style.visibility === "hidden") return "";
            const value = style.content;
            if (value === "" || value === "none" || value === "normal") return "";
            const text = contentTextOf(el, value);
            if (text === null) return "";
            return style.display === "inline" ? text : " " + text + " ";
          };
          // Nested named-from-content roles walk the same descendants again, so
          // each element's style, and each pseudo element, is read once.
          const styles = new Map();
          const styleOf = (el) => {
            let style = styles.get(el);
            if (!style) styles.set(el, style = getComputedStyle(el));
            return style;
          };
          const generated = new Map();
          const generatedOf = (el, pseudo) => {
            let entry = generated.get(el);
            if (!entry) generated.set(el, entry = {});
            if (!(pseudo in entry)) entry[pseudo] = generatedContentOf(el, pseudo);
            return entry[pseudo];
          };
          // A subtree the name computation drops: aria-hidden, or hidden by style as innerText leaves it out.
          const isNameHidden = (el, style) =>
            ariaHidden(el) || style.display === "none" || style.visibility === "hidden";
          // alt of an element HTML-AAM names by it: an img or an input type="image".
          const altOf = (el) => {
            const named = el instanceof HTMLImageElement || (el instanceof HTMLInputElement && el.type === "image");
            const alt = named ? el.getAttribute("alt") : null;
            return alt !== null && alt.trim() !== "" ? alt.trim() : null;
          };
          // The <title> child an svg is named by (SVG-AAM), as Playwright reads
          // it. An svg marked presentation or none takes no name of its own from
          // it, though the title still names a link or button around it.
          const svgTitleOf = (el) => {
            if (!(el instanceof SVGElement)) return null;
            const title = Array.from(el.children).find((child) => child instanceof SVGTitleElement);
            const text = (title?.textContent ?? "").replace(/\s+/g, " ").trim();
            return text === "" ? null : text;
          };
          const isPresentational = (el) => {
            const role = roleOf(el);
            return role === "presentation" || role === "none";
          };
          // A referenced target accname 2A reads whole: hidden itself, or under an aria-hidden ancestor.
          // Upstream's hidden includes a box with no size, which a target under display: none has.
          const isReferenceHidden = (el) => {
            if (hidden(el) || el.closest("[aria-hidden=\"true\" i]") !== null) return true;
            const box = el.getBoundingClientRect();
            return !(box.width > 0 && box.height > 0);
          };
          // An IDREF resolves in the tree scope it is written in.
          const referencedElementOf = (el, id) => {
            const root = el.getRootNode();
            return root instanceof Document || root instanceof DocumentFragment ? root.getElementById(id) : null;
          };
          // Each aria-labelledby target's contribution (accname 2B), in attribute
          // order, unnamed targets dropped; null when their joined text is empty.
          const referencedNamesOf = (el, walk) => {
            if (walk.inReference) return null;
            const ids = (el.getAttribute("aria-labelledby") || "").trim();
            if (ids === "") return null;
            const contributions = [];
            for (const id of ids.split(/\s+/)) {
              const target = referencedElementOf(el, id);
              if (target === null) continue;
              contributions.push(contentNameOf(target, styleOf(target), {
                inReference: true,
                hiddenAllowed: isReferenceHidden(target),
                visited: walk.visited,
                accessible: walk.accessible,
              }));
            }
            if (contributions.join(" ") === "") return null;
            return contributions.map((text) => text.replace(/\s+/g, " ").trim()).filter((name) => name !== "");
          };
          const isSecureField = (el) => el instanceof HTMLInputElement && (el.type === "password" || el.getAttribute("autocomplete") === "current-password");
          // Every element under el and every element its aria-owns names, with theirs.
          const ariaOwnedOf = (el) => {
            const owned = Array.from(el.querySelectorAll("*"));
            for (const id of (el.getAttribute("aria-owns") || "").trim().split(/\s+/)) {
              const target = id === "" ? null : referencedElementOf(el, id);
              if (target !== null) owned.push(target, ...Array.from(target.querySelectorAll("*")));
            }
            return owned;
          };
          // What an embedded control contributes to a name computed through
          // another element (accname 2C): a text field its value, a combobox or
          // listbox its selected options, a range widget its value. Null for any
          // other element, and for one its own aria-labelledby names.
          const embeddedControlNameOf = (el, walk) => {
            const role = roleOf(el);
            if (role === null) return null;
            if (el.id !== "" && (el.getAttribute("aria-labelledby") || "").trim().split(/\s+/).indexOf(el.id) !== -1) return null;
            if (isSecureField(el)) return "";
            if (role === "textbox" || role === "searchbox") {
              return el instanceof HTMLInputElement || el instanceof HTMLTextAreaElement ? el.value : el.textContent ?? "";
            }
            if (role === "combobox" || role === "listbox") {
              let selected;
              if (el instanceof HTMLSelectElement) {
                selected = Array.from(el.selectedOptions);
                if (selected.length === 0 && el.options.length > 0) selected.push(el.options[0]);
              } else {
                const listbox = role === "combobox" ? ariaOwnedOf(el).find((owned) => roleOf(owned) === "listbox") : el;
                selected = listbox === undefined
                  ? []
                  : ariaOwnedOf(listbox).filter((owned) => owned.getAttribute("aria-selected") === "true" && roleOf(owned) === "option");
              }
              if (selected.length === 0 && el instanceof HTMLInputElement) return el.value;
              return selected.map((option) => contentNameOf(option, styleOf(option), walk)).join(" ");
            }
            if (role === "progressbar" || role === "scrollbar" || role === "slider" || role === "spinbutton" || role === "meter") {
              if (el.hasAttribute("aria-valuetext")) return el.getAttribute("aria-valuetext") ?? "";
              if (el.hasAttribute("aria-valuenow")) return el.getAttribute("aria-valuenow") ?? "";
              return el.getAttribute("value") ?? "";
            }
            return role === "menu" ? "" : null;
          };
          // What one element contributes to a name computed through another, as
          // an aria-labelledby target (2B) or a descendant read for name from
          // content (2F): its references, its value as an embedded control, its
          // aria-label, its alt or svg title, its children, then its title.
          const contentNameOf = (el, style, walk) => {
            if (walk.visited.has(el)) return "";
            walk.visited.add(el);
            if (!walk.hiddenAllowed && isNameHidden(el, style)) return "";
            const referenced = referencedNamesOf(el, walk);
            if (referenced !== null) return referenced.join(" ");
            const embedded = walk.accessible ? embeddedControlNameOf(el, walk) : null;
            if (embedded !== null) return embedded;
            const ariaLabel = el.getAttribute("aria-label");
            if (ariaLabel !== null && ariaLabel.trim() !== "") return ariaLabel.trim();
            const alternative = altOf(el) ?? svgTitleOf(el);
            if (alternative !== null) return alternative;
            if (nameOpaqueTags.has(el.tagName)) return "";
            const content = childrenNameOf(el, walk);
            if (content !== "") return content;
            const title = el.getAttribute("title");
            return title === null ? "" : title.trim();
          };
          // The children's contributions joined as Playwright's role selector
          // joins them: a space on each side of a block-level child and of a
          // <br>, none around an inline one. An accessible name wraps them in
          // the element's generated content.
          const childrenNameOf = (el, walk) => {
            let out = walk.accessible ? generatedOf(el, "::before") : "";
            for (const child of contentChildrenOf(el)) {
              if (child.nodeType === 3) {
                out += child.nodeValue ?? "";
                continue;
              }
              if (!(child instanceof Element)) continue;
              const style = styleOf(child);
              const token = contentNameOf(child, style, walk);
              const block = child.tagName === "BR" || style.display !== "inline";
              out += block ? " " + token + " " : token;
            }
            return walk.accessible ? out + generatedOf(el, "::after") : out;
          };
          // A slot reads what is assigned to it; a host reads its light children
          // (a slotted one skipped, its slot reads it) and then its shadow tree.
          const contentChildrenOf = (el) => {
            if (el instanceof HTMLSlotElement) {
              const assigned = el.assignedNodes();
              if (assigned.length > 0) return assigned;
            }
            const shadow = shadowOf(el);
            if (shadow === null) return Array.from(el.childNodes);
            const slotted = new Set();
            for (const slot of Array.from(shadow.querySelectorAll("slot"))) {
              for (const node of slot.assignedNodes()) slotted.add(node);
            }
            const own = Array.from(el.childNodes).filter((child) => !slotted.has(child));
            return own.concat(Array.from(shadow.childNodes));
          };
          // Text for a name from an element's own content (accname 2F). named is
          // the element the text names when that is not el (a control read
          // through its <label>), which never contributes to its own name.
          const nameTextOf = (el, accessible, named = el) => {
            if (isNameHidden(el, styleOf(el))) return "";
            if (nameOpaqueTags.has(el.tagName)) return "";
            return childrenNameOf(el, nameWalk([el, named], accessible)).replace(/\s+/g, " ").trim();
          };
          const nameOpaqueTags = new Set(["TEXTAREA", "SELECT", "INPUT", "SCRIPT", "STYLE"]);
          // Roles named from their content (accname 2F, the list Playwright's
          // role selector uses), plus listitem, status, and alert.
          const nameFromContentRoles = new Set([
            "button", "cell", "checkbox", "columnheader", "gridcell", "heading", "link", "menuitem", "menuitemcheckbox",
            "menuitemradio", "option", "radio", "row", "rowheader", "switch", "tab", "tooltip", "treeitem", "listitem", "status", "alert"
          ]);
          // HTML-AAM: the child element that names its parent when nothing ARIA does.
          const namingChildTags = { FIELDSET: "LEGEND", FIGURE: "FIGCAPTION", TABLE: "CAPTION" };
          const placeholderNamedInputTypes = ["text", "password", "number", "search", "tel", "email", "url"];
          // The controls a placeholder may name: text-like inputs, textareas, and the textbox and searchbox roles.
          const isPlaceholderNamed = (el, role) => {
            if (el instanceof HTMLTextAreaElement) return true;
            if (el instanceof HTMLInputElement) return placeholderNamedInputTypes.indexOf(el.type) !== -1;
            return role === "textbox" || role === "searchbox";
          };
          // The placeholder a rich-text editor paints from data-placeholder: Quill
          // sets it on the host; ProseMirror and TipTap set it on the first empty
          // block and drop it once the document has content, as the painted hint goes.
          const editorPlaceholderOf = (host) => {
            const own = host.getAttribute("data-placeholder");
            if (own !== null && own.trim() !== "") return own.trim();
            for (const block of host.querySelectorAll("[data-placeholder]")) {
              if ((block.textContent || "").trim() !== "") continue;
              const placeholder = block.getAttribute("data-placeholder");
              if (placeholder !== null && placeholder.trim() !== "") return placeholder.trim();
            }
            return null;
          };
          const accessibleName = (el, role) => {
            // accname reads a labelledby reference (2B) before the element's own aria-label (2C).
            const referenced = referencedNamesOf(el, nameWalk([], true));
            if (referenced !== null) return referenced.join(" ");
            const ariaLabel = el.getAttribute("aria-label");
            if (ariaLabel !== null && ariaLabel.trim() !== "") return ariaLabel.trim();
            const labels = el.labels ? Array.from(el.labels) : [];
            if (labels.length > 0) {
              const joined = labels.map((label) => nameTextOf(label, true, el)).join(" ").trim();
              if (joined !== "") return joined;
            }
            const alternative = altOf(el) ?? (isPresentational(el) ? null : svgTitleOf(el));
            if (alternative !== null) return alternative;
            const captionTag = namingChildTags[el.tagName];
            if (captionTag !== undefined) {
              const caption = Array.from(el.children).find((child) => child.tagName === captionTag);
              const text = caption === undefined ? "" : nameTextOf(caption, true);
              if (text !== "") return text;
            }
            if (el instanceof HTMLInputElement && (el.type === "button" || el.type === "submit" || el.type === "reset")) {
              if (el.value.trim() !== "") return el.value.trim();
              // HTML-AAM: a submit or reset button with no value reads its default label.
              if (el.type === "submit") return "Submit";
              if (el.type === "reset") return "Reset";
            }
            if (role !== null && nameFromContentRoles.has(role)) {
              const text = nameTextOf(el, true);
              if (text !== "") return text;
            }
            const title = el.getAttribute("title");
            if (title !== null && title.trim() !== "") return title.trim();
            // HTML-AAM names an unlabeled text control by its placeholder, after the title.
            if (isPlaceholderNamed(el, role)) {
              const placeholder = el.getAttribute("placeholder");
              if (placeholder !== null && placeholder.trim() !== "") return placeholder.trim();
              const ariaPlaceholder = el.getAttribute("aria-placeholder");
              if (ariaPlaceholder !== null && ariaPlaceholder.trim() !== "") return ariaPlaceholder.trim();
              if (isEditingHost(el)) return editorPlaceholderOf(el);
            }
            return null;
          };
          // No name is null, as upstream reports it, so the snapshot shows the node's text instead.
          const nameOf = (el, role) => {
            const name = reportedName(el, accessibleName(el, role));
            return name === null ? null : cut(name, 256);
          };
          // The roles aria-disabled applies to (WAI-ARIA 1.2), as Playwright's
          // toBeDisabled reads them.
          const ariaDisabledRoles = new Set([
            "application", "button", "composite", "gridcell", "group", "input", "link", "menuitem", "scrollbar",
            "separator", "tab", "checkbox", "columnheader", "combobox", "grid", "listbox", "menu", "menubar",
            "menuitemcheckbox", "menuitemradio", "option", "radio", "radiogroup", "row", "rowheader", "searchbox",
            "select", "slider", "spinbutton", "switch", "tablist", "textbox", "toolbar", "tree", "treegrid", "treeitem"
          ]);
          const parentOrHostOf = (el) => {
            if (el.parentElement) return el.parentElement;
            const root = el.getRootNode();
            return root instanceof ShadowRoot ? root.host : null;
          };
          // The nearest aria-disabled on the element or above it; "false" cuts the chain.
          const ariaDisabledInChain = (el) => {
            for (let current = el; current; current = parentOrHostOf(current)) {
              const value = (current.getAttribute("aria-disabled") || "").toLowerCase();
              if (value === "true") return true;
              if (value === "false") return false;
            }
            return false;
          };
          // :disabled covers the control's own attribute, a disabled fieldset
          // (outside its first legend), and a disabled optgroup. aria-disabled
          // on the element counts for any role; inherited, it reaches only the
          // roles the state applies to.
          const disabledOf = (el, role) => {
            if (el.matches(":disabled")) return true;
            if (el.getAttribute("aria-disabled") === "true") return true;
            return !!role && ariaDisabledRoles.has(role) && ariaDisabledInChain(el);
          };
          // A native checkbox or radio reports its own state; aria-checked
          // fills in only where the element has no native state.
          const checkedOf = (el) => {
            if (el instanceof HTMLInputElement && (el.type === "checkbox" || el.type === "radio")) return el.checked;
            return el.getAttribute("aria-checked") === "true";
          };
          // Whether the element reports a checked state at all: a native checkbox or radio, or an aria-checked attribute.
          const checkedReportedOf = (el) => (el instanceof HTMLInputElement && (el.type === "checkbox" || el.type === "radio")) || el.getAttribute("aria-checked") !== null;
          // The mixed state of a checkbox (indeterminate, or aria-checked) or a
          // button (aria-pressed): a role query for either value skips it.
          const mixedOf = (el) => el instanceof HTMLInputElement && el.type === "checkbox"
            ? el.indeterminate
            : el.getAttribute("aria-checked") === "mixed" || el.getAttribute("aria-pressed") === "mixed";
          // aria-level wins; an h1 to h6 reads its tag; any other heading is level 2 (HTML-AAM).
          const levelOf = (el, role) => {
            const tagLevel = /^H[1-6]$/.test(el.tagName) ? Number(el.tagName.slice(1)) : null;
            if (role !== "heading") return tagLevel;
            const ariaLevel = Number.parseInt(el.getAttribute("aria-level") || "", 10);
            return Number.isInteger(ariaLevel) && ariaLevel > 0 ? ariaLevel : tagLevel ?? 2;
          };
          // A link target as the model reads it: origin and path, without
          // userinfo, query, or fragment, which routinely carry tokens. A
          // dropped query or fragment leaves "?…" or "#…" behind, so the value
          // never reads as the whole target; a URL path never holds a literal
          // "…", since the URL parser percent-encodes it. mailto: and tel: keep
          // their scheme in place of an origin; blob: keeps its inner URL,
          // reduced the same way, or becomes "blob:…" when that has no origin.
          // Any other scheme with no origin (data:, javascript:) carries its
          // payload as its path, so it is reduced to "scheme:…". The path is
          // whole otherwise: the agent bounds what it renders.
          const originAndPath = (value, base) => {
            const elided = (query, fragment) => (query ? "?…" : "") + (fragment ? "#…" : "");
            try {
              const url = new URL(value, base);
              const rest = url.pathname + elided(url.search !== "", url.hash !== "");
              if (url.protocol === "blob:") {
                let inner = null;
                try { inner = new URL(url.pathname); } catch { inner = null; }
                return inner === null || inner.origin === "null" ? "blob:…" : "blob:" + inner.origin + inner.pathname + elided(url.search !== "", url.hash !== "");
              }
              if (url.origin !== "null") return url.origin + rest;
              if (url.protocol === "mailto:" || url.protocol === "tel:") return url.protocol + rest;
              return url.protocol + "…";
            } catch {
              const [beforeFragment, ...fragment] = value.split("#");
              const [path, ...query] = beforeFragment.split("?");
              return path + elided(query.join("?") !== "", fragment.join("#") !== "");
            }
          };
          const attributesOf = (el, secure, testIdAttribute) => {
            const out = {};
            for (const attr of el.attributes) {
              if (secure && attr.name === "value") continue;
              if (attr.name === testIdAttribute) continue;
              // Observations expose href origin and path only: query strings and
              // fragments routinely carry tokens.
              out[attr.name] = attr.name === "href" ? originAndPath(attr.value, el.ownerDocument.baseURI) : attr.value;
            }
            return out;
          };
          // No layout box at all (display: none on it or an ancestor, any
          // display: contents element) is no rect, as Playwright's
          // boundingBox() answers null; a box with no size, or one
          // visibility: hidden keeps, is still a rect.
          const rectOf = (el) => {
            if (el.getClientRects().length === 0) return null;
            const box = el.getBoundingClientRect();
            return { x: box.x, y: box.y, width: box.width, height: box.height };
          };
          // The element's padding-box corner, when a border puts it past the
          // rect's: where an offset into the element is measured from.
          const originOf = (el) => {
            if (el.getClientRects().length === 0) return null;
            const style = getComputedStyle(el);
            const borderLeft = Number.parseFloat(style.borderLeftWidth) || 0;
            const borderTop = Number.parseFloat(style.borderTopWidth) || 0;
            if (borderLeft === 0 && borderTop === 0) return null;
            const box = el.getBoundingClientRect();
            return { x: box.x + borderLeft, y: box.y + borderTop };
          };
          // The nodes an element renders in its place: a shadow host's shadow
          // tree, a slot's assigned nodes or else its fallback content, any
          // other element's children.
          const renderedChildrenOf = (el) => {
            const shadow = shadowOf(el);
            if (shadow) return [...shadow.childNodes];
            if (el instanceof HTMLSlotElement) {
              const assigned = el.assignedNodes();
              if (assigned.length > 0) return assigned;
            }
            return [...el.childNodes];
          };
          // What a label query finds an element by, as Playwright's getByLabel does: its aria-label, the elements its
          // aria-labelledby names, and the labels it is associated with. Its content, title, and placeholder are no label.
          const labelsOf = (el) => {
            const labels = [];
            const ariaLabel = el.getAttribute("aria-label");
            if (ariaLabel !== null && ariaLabel.trim() !== "") labels.push(ariaLabel.trim());
            for (const name of referencedNamesOf(el, nameWalk([], false)) ?? []) labels.push(name);
            for (const label of el.labels ? Array.from(el.labels) : []) {
              const text = nameTextOf(label, false);
              if (text !== "") labels.push(text);
            }
            return labels.map(withoutGlyphs).filter((label) => label !== "");
          };
          // An element's DOM text as Playwright's text engine reads it: every descendant's text in place, with no
          // separator where one block ends and the next begins (innerText puts a line break there), and a button
          // input's value standing for its text. Script and style text is no text. Read to a bound, then cut.
          const domTextBound = 8192;
          const domTextOf = (el, limit) => {
            let out = "";
            const read = (node) => {
              if (out.length > domTextBound) return;
              if (node.nodeType === 3) out += node.nodeValue ?? "";
              if (node.nodeType !== 1 || skip.has(node.tagName)) return;
              if (node instanceof HTMLInputElement && (node.type === "button" || node.type === "submit")) {
                out += node.value;
                return;
              }
              for (const child of renderedChildrenOf(node)) read(child);
            };
            read(el);
            return cut(out, limit);
          };
        """;

    /// <summary>
    /// Upstream's <c>read(element, { mode: 'hidden' })</c>: whether one element reads as shown, as the tree reads it. Port-only name:
    /// upstream's reader is one function with a mode, this one a fragment that follows <see cref="Prelude"/>. A node is hidden when
    /// it is (<c>hidden</c>), or an ancestor in the flat tree hides its subtree (<c>display: none</c>, <c>content-visibility: hidden</c>),
    /// which is the verdict the tree walk carries down as <c>parentHidden</c>; it is not shown when it lays out a box with no size
    /// either (the tree's <c>emptyBox</c>), except an <c>option</c>. The engine's own <c>isNodeVisible</c> (<c>!Hidden &amp;&amp; !EmptyBox</c>)
    /// reads the same two facts.
    /// </summary>
    public const string HiddenMode = """
          const shownInTree = (el) => {
            for (let parent = parentOrHostOf(el); parent !== null; parent = parentOrHostOf(parent)) {
              if (hidesSubtree(parent, getComputedStyle(parent))) return false;
            }
            if (hidden(el)) return false;
            if (el.tagName === "OPTION") return true;
            const box = rectOf(el);
            return box === null || (box.width > 0 && box.height > 0);
          };
        """;
}
