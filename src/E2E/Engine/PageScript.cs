// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Engine;

internal static class PageScript
{
    // Each element keeps its ref under a registry symbol for the life of the
    // document, so a ref the model saw stays valid in the next observation.
    // The host passes the next free number, so refs never repeat across
    // documents. The latest observation's elements are kept for RefsOrForce. The
    // walk reads one document: it goes through open shadow roots and closed
    // ones the init script recorded, and lists an iframe as a boundary node
    // the host fills with that frame's own observation. When the document has
    // more nodes than the budget, every node that intersects the viewport is
    // kept first and the rest of the budget goes to the others in document
    // order, so scrolling brings the cut ones in. It says when it cut nodes.
    // The helpers that read one element (role, name, state, labels, hidden) are
    // ReadSemantics.Prelude, declared first; what follows is the tree reader:
    // the budget, the refs, the line text, and the walk that carries the
    // hidden state down.
    public const string Collect = $$"""
        ({ seed, max, testIdAttribute, publish }) => {
        {{ReadSemantics.Prelude}}
          const refKey = Symbol.for("e2e.observation.ref");
          const elements = new Map();
          let next = seed;
          const stamp = (el) => {
            let id = el[refKey];
            if (id === undefined) {
              id = "e" + next;
              next++;
              el[refKey] = id;
            }
            elements.set(id, el);
            return id;
          };
          // The elements a locator's selector named that this tree does not list (upstream reads any element the selector matches).
          // The host sets them just before an observation that publishes, which takes them: they are listed as nodes like the rest.
          const forcedKey = Symbol.for("e2e.observation.forced");
          const forced = publish ? globalThis[forcedKey] ?? null : null;
          if (publish) delete globalThis[forcedKey];
          const isForced = (el) => forced !== null && forced.has(el);
          // The ancestors of the marked elements, shadow hosts included: a leaf lists no children, unless a selector named one of them.
          const holders = new Set();
          if (forced !== null) {
            for (const named of forced) {
              for (let node = named.parentNode ?? named.host ?? null; node && !holders.has(node); node = node.parentNode ?? node.host ?? null) holders.add(node);
            }
          }
          const holdsForced = (el) => holders.has(el);
          const leaves = new Set(["button", "link", "textbox", "checkbox", "radio", "searchbox", "heading", "status", "image", "tab",
            "option", "menuitem", "menuitemcheckbox", "menuitemradio", "switch", "slider", "spinbutton", "progressbar", "meter", "separator", "iframe"]);
          // Port only: upstream has no leaf set, since its walk lists every child. An
          // editing host lists its children (the editor's document, and the editors nested in it).
          const isLeaf = (el, role) => !!role && leaves.has(role) && !isEditingHost(el);
          const maxSelectOptions = 60;
          const textLimit = 512;
          let count = 0;
          let textCount = 0;
          let textCut = false;
          let full = false;
          let gate = null;
          let offBudget = max;
          let offCount = 0;
          let scroll = Math.round(scrollX) + "," + Math.round(scrollY);
          let focused = document.activeElement;
          while (focused) {
            const inner = shadowOf(focused)?.activeElement ?? null;
            if (!inner) break;
            focused = inner;
          }
          // An option paints inside its select, so it is on screen when the select is.
          const inView = (el) => {
            const box = el.tagName === "OPTION" ? (el.closest("select") ?? el) : el;
            const r = box.getBoundingClientRect();
            return r.bottom > 0 && r.right > 0 && r.top < innerHeight && r.left < innerWidth;
          };
          // Text owned directly by an element, not by its child elements. An
          // element with content-visibility: hidden renders none of it. The
          // budget tally and the walk both ask, so each answer is kept.
          const directTexts = new Map();
          const directTextOf = (el) => {
            if (directTexts.has(el)) return directTexts.get(el);
            let out = "";
            for (const child of el.childNodes) {
              if (child.nodeType === 3) out += child.nodeValue ?? "";
            }
            out = out.replace(/\s+/g, " ").trim();
            if (out && getComputedStyle(el).getPropertyValue("content-visibility") === "hidden") out = "";
            directTexts.set(el, out);
            return out;
          };
          const memoized = (read) => {
            const answers = new Map();
            return (el) => {
              if (!answers.has(el)) answers.set(el, read(el));
              return answers.get(el);
            };
          };
          // Elements whose content is not text in a line: a form control's
          // options or value, a picture, a frame, a drawn surface. Like a line
          // break, each separates the words around it.
          const lineBreaking = new Set(["SCRIPT", "STYLE", "NOSCRIPT", "TEMPLATE", "SVG", "MATH", "CANVAS", "VIDEO", "AUDIO",
            "IFRAME", "IMG", "INPUT", "SELECT", "TEXTAREA", "BR"]);
          // Render visibility as the walk reads it: hidden, or a box with no
          // size, which shows no words.
          const invisible = (el, style) => {
            if (hidden(el, style)) return true;
            if (style.display === "contents") return false;
            const box = el.getBoundingClientRect();
            return !(box.width > 0 && box.height > 0);
          };
          // True for a box laid out in its parent's line, or for display:
          // contents, which lays out none of its own.
          const isInlineLevel = (style) =>
            style.display.startsWith("inline") || style.display.startsWith("ruby") || style.display === "contents";
          // True when an element's text flows in its parent's line: a visible
          // inline-level box. A slot has no box and shows the nodes assigned to
          // it, so only its visibility counts: a hidden one hides the text they
          // inherit it with, and a child that shows itself again is listed on
          // its own.
          const flowsInLine = memoized((el) => {
            if (lineBreaking.has(el.tagName.toUpperCase()) || isEditingHost(el)) return false;
            const style = styleOf(el);
            if (ariaHidden(el) || isInert(el) || hidesSubtree(el, style)) return false;
            if (el instanceof HTMLSlotElement ? style.visibility !== "visible" : invisible(el, style)) return false;
            return isInlineLevel(style);
          });
          // An element's rendered text with each inline descendant's text in
          // place. Anything else in the line reads as a space, except a hidden
          // inline element (a <wbr>, a zero-size span), which shows nothing.
          const lineRunOf = (el) => {
            let out = "";
            for (const child of renderedChildrenOf(el)) {
              if (child.nodeType === 3) out += child.nodeValue ?? "";
              if (child.nodeType !== 1) continue;
              if (flowsInLine(child)) {
                out += lineRunOf(child);
                continue;
              }
              const style = styleOf(child);
              if (ariaHidden(child) || isInert(child) || hidesSubtree(child, style)) continue;
              if (lineBreaking.has(child.tagName.toUpperCase()) || !isInlineLevel(style) || !invisible(child, style)) out += " ";
            }
            return out;
          };
          // True when an element with no role still says it can be acted on:
          // focusable through tabindex, a click handler set as an attribute or
          // a property, or an <a> with no href, which an app wires up by
          // script. Such an element stays listed inside a line, so it keeps an
          // id to act on. A handler added with addEventListener leaves nothing
          // the page can read.
          const offersAction = memoized((el) => {
            const tabindex = el.getAttribute("tabindex");
            if (tabindex !== null && /^\s*[-+]?\d/.test(tabindex)) return true;
            if (el.tagName === "A" || el.hasAttribute("onclick")) return true;
            return el instanceof HTMLElement && el.onclick !== null;
          });
          // True when an element reads its own line: it has text of its own,
          // or it offers an action inside an ancestor's line, where its words
          // are what a person aims at.
          const ownsLine = (el) => directTextOf(el) !== "" || (offersAction(el) && isReadInLine(el));
          // The text an element owns, read as a person reads its line: its own
          // text with every inline descendant's text in place, so a link in a
          // sentence stays a node of its own while the sentence reads whole.
          // Block-level descendants are nodes of their own and only separate
          // words. An element with no text of its own owns none.
          const lineTextOf = memoized((el) => ownsLine(el) ? lineRunOf(el).replace(/\s+/g, " ").trim() : "");
          // True when an element's text is already read in an ancestor's line,
          // so the element has nothing to add on text alone. A line longer
          // than the text bound is cut, so its inline descendants stay listed
          // and nothing past the cut is lost.
          const isReadInLine = memoized((el) => {
            const parent = el.assignedSlot ?? parentOrHostOf(el);
            if (parent === null || !flowsInLine(el)) return false;
            if (!ownsLine(parent)) return isReadInLine(parent);
            return lineTextOf(parent).length <= textLimit && !invisible(parent, styleOf(parent));
          });
          // As upstream's tree, an element is listed by its role, its test id,
          // or text of its own, so text inside a bare span, a strong or a label is a
          // node a text query can answer with. An SVG element has no innerText to
          // read, so it is not listed by text.
          const ownsText = (el) => el instanceof HTMLElement &&
            (offersAction(el) && isReadInLine(el) ? lineTextOf(el) !== "" : directTextOf(el) !== "" && !isReadInLine(el));
          const hasRole = (role) => !!role && role !== "presentation" && role !== "none";
          // An iframe is a boundary node whatever its role: upstream emits it before any role check,
          // so <iframe role="none"> stays listed, with the role none.
          const listed = (el, role) => el.tagName === "IFRAME" || hasRole(role) || !!el.getAttribute(testIdAttribute) || ownsText(el) || isForced(el);
          // A node listed for its text alone. It draws on its own allowance, so
          // a page full of text cannot push controls out of the snapshot.
          const textOnly = (el, role) => el.tagName !== "IFRAME" && !hasRole(role) && !el.getAttribute(testIdAttribute) && !isForced(el);
          // The inline elements read in the element's line that have no node of
          // their own, as bare nodes of text under it, so a text query still
          // finds a word the line carries, as Playwright's text engine does.
          // They carry their attributes, so a read of the match finds its id.
          // They stay out of the tree the agent reads.
          const inlineNodesOf = (parent, isHidden) => {
            const out = [];
            for (const child of renderedChildrenOf(parent)) {
              if (child.nodeType !== 1 || !flowsInLine(child) || listed(child, roleOf(child))) continue;
              const text = cut(lineRunOf(child), Infinity);
              if (text) out.push({ ref: stamp(child), text, domText: domTextOf(child, textLimit), hidden: isHidden, attributes: attributesOf(child, false, testIdAttribute), rect: rectOf(child), children: inlineNodesOf(child, isHidden) });
            }
            return out;
          };
          // How many nodes the walk would list, or only those on screen. Text-only nodes are not counted.
          // Chrome drops an inert subtree from its accessibility tree, and so does the walk. A slotted
          // element is inert when a shadow ancestor of its slot is, which a closed root hides from assignedSlot.
          const isInert = (el) => {
            if (el.inert || styleOf(el).getPropertyValue("interactivity") === "inert") return true;
            const shadow = el.parentElement && shadowOf(el.parentElement);
            const name = el.getAttribute("slot");
            const slot = shadow?.querySelector(name ? `slot[name="${CSS.escape(name)}"]` : "slot:not([name])");
            for (let node = slot; node && node !== shadow; node = node.parentNode) if (node.inert) return true;
            return false;
          };
          // A closed select has no option boxes; explicit exclusions still remove choices it does not offer.
          const isOfferedOption = (option) => {
            for (let current = option; current && current.tagName !== "SELECT"; current = parentOrHostOf(current)) {
              const style = styleOf(current);
              if (ariaHidden(current) || isInert(current) || hidesSubtree(current, style)) return false;
            }
            return true;
          };
          const tally = (el, visible) => {
            if (!el || (skip.has(el.tagName) && !isForced(el)) || isInert(el)) return 0;
            if (el.tagName === "IFRAME" && (ariaHidden(el) || hidden(el))) return 0;
            let n = 0;
            const role = roleOf(el);
            if (listed(el, role) && !textOnly(el, role)) {
              const counted = !visible || inView(el);
              if (counted) n++;
              if (el.tagName === "SELECT") {
                if (!counted) return n;
                let offered = 0;
                for (const option of el.options) {
                  if (offered >= maxSelectOptions) break;
                  if (isOfferedOption(option)) offered += 1;
                }
                return n + offered;
              }
              if (isLeaf(el, role) && !holdsForced(el)) return n;
            }
            for (const child of el.children) n += tally(child, visible);
            const shadow = shadowOf(el);
            if (shadow) for (const child of shadow.children) n += tally(child, visible);
            return n;
          };
          // A node under an ancestor that hides its subtree is hidden too.
          // parentHidden is that ancestor's verdict, not its own visibility.
          // parentAria is whether an ancestor is aria-hidden: the node paints
          // and is visible, but the accessibility tree drops it.
          const walk = (el, into, parentHidden, parentAria) => {
            if (!el || (skip.has(el.tagName) && !isForced(el)) || isInert(el) || (full && (forced === null || (!holders.has(el) && !isForced(el))))) return;
            if (gate !== null && !gate(el)) return;
            const style = getComputedStyle(el);
            const isHidden = !!parentHidden || hidden(el, style);
            const isAriaHidden = !!parentAria || ariaHidden(el);
            const childrenHidden = !!parentHidden || hidesSubtree(el, style);
            const role = roleOf(el);
            const testId = el.getAttribute(testIdAttribute);
            const isFrame = el.tagName === "IFRAME";
            // A frame the page hides is no boundary of the tree, unless a frame locator named it: then the host reads its document as hidden.
            if (isFrame && (isHidden || isAriaHidden) && !isForced(el)) return;
            if (listed(el, role)) {
              const textNode = textOnly(el, role);
              // A node a selector named is read whatever the budget says: the locate must reach what it names.
              const named = isForced(el);
              if (!named && (textNode ? textCount >= max : count >= max)) {
                if (!textNode) {
                  full = true;
                  // The walk goes on to the elements a selector named, which are listed past the cut.
                  if (holdsForced(el)) walkChildren(el, into, childrenHidden, isAriaHidden);
                  return;
                }
                textCut = true;
                walkChildren(el, into, childrenHidden, isAriaHidden);
                return;
              }
              if (!named && !textNode && offBudget < max && !inView(el)) {
                if (offCount >= offBudget) {
                  if ((isLeaf(el, role) && !holdsForced(el)) || el.tagName === "SELECT") return;
                  walkChildren(el, into, childrenHidden, isAriaHidden);
                  return;
                }
                offCount++;
              }
              if (textNode) textCount++;
              else count++;
              // An editor's document is its value, as a textarea's text is: kept as
              // rendered, spaces and newlines included. An empty editor renders
              // <p><br></p>, which innerText reads as a newline, so a document with no
              // text nodes, or with only markup whitespace that renders as that one
              // newline, is an empty value; typed spaces are text nodes and stay. The
              // zero-width no-break space Slate and Quill pad an empty line with
              // renders nothing, so it is no text either.
              let editorValue = null;
              if (isEditingHost(el)) {
                const rendered = el.innerText.replace(/\uFEFF/g, "");
                const text = (el.textContent || "").replace(/\uFEFF/g, "");
                editorValue = text === "" || (text.trim() === "" && rendered === "\n") ? "" : rendered;
              }
              const type = (el.getAttribute("type") || "").toLowerCase();
              const secure = type === "password" || el.getAttribute("autocomplete") === "current-password";
              const ownsChildren = !isLeaf(el, role) && el.tagName !== "SELECT" && !isFrame;
              const node = {
                ref: stamp(el),
                role,
                name: isFrame ? cut(el.getAttribute("title") || "", 256) || null : nameOf(el, role),
                // A leaf keeps its own text too: a labelled status or button
                // reads its content, not its label, as upstream's node read
                // does. A secure field withholds it. innerText is empty for a
                // node that does not render, and for a textarea; its DOM text is
                // what a text query matches.
                text: secure ? "" : cut((isHidden || el.tagName === "TEXTAREA" ? el.textContent : el.innerText) || "", 512),
                // A node that lists its children shows the snapshot only the
                // text it owns, read as a line: its inline words stay in place.
                ownText: secure || !ownsChildren ? null : cut(lineTextOf(el), textLimit),
                // A leaf lists no children, but a text query still finds the words it holds in an inline element.
                inline: secure || isFrame || el.tagName === "SELECT" || lineTextOf(el) === "" ? null : inlineNodesOf(el, isHidden),
                domText: secure ? "" : domTextOf(el, textLimit),
                labels: labelsOf(el),
                value: secure ? null : el instanceof HTMLInputElement || el instanceof HTMLTextAreaElement || el instanceof HTMLSelectElement ? String(el.value ?? "") : editorValue,
                selection: el === focused && !secure ? selectedTextOf(el)?.slice(0, textLimit) ?? null : null,
                testId,
                placeholder: el.getAttribute("placeholder"),
                inputPurpose: secure ? "password" : null,
                level: levelOf(el, role),
                disabled: disabledOf(el, role),
                checked: checkedOf(el),
                checkedReported: checkedReportedOf(el),
                mixed: mixedOf(el),
                // A box with no width or height; an option has none while its select is closed.
                emptyBox: !isHidden && el.tagName !== "OPTION" && (() => { const box = rectOf(el); return box !== null && (box.width <= 0 || box.height <= 0); })(),
                expandedAbsent: !["true", "false"].includes(el.getAttribute("aria-expanded") || "") && el.tagName !== "DETAILS",
                expanded: el.getAttribute("aria-expanded") === "true" || (el.tagName === "DETAILS" && el.open),
                selected: el.tagName === "OPTION" ? !!el.selected : (el.getAttribute("aria-selected") || "").toLowerCase() === "true",
                pressed: el.getAttribute("aria-pressed") === "true",
                focused: el === focused,
                hidden: isHidden,
                ariaHidden: isAriaHidden,
                secure,
                frame: isFrame,
                attributes: attributesOf(el, secure, testIdAttribute),
                // Upstream's node read keeps the whole href; here every read comes from this tree.
                wholeHref: el.getAttribute("href"),
                rect: rectOf(el),
                origin: originOf(el),
                children: []
              };
              into.push(node);
              if (el.scrollTop || el.scrollLeft) scroll += ";" + node.ref + ":" + Math.round(el.scrollLeft) + "," + Math.round(el.scrollTop);
              if (el.tagName === "SELECT") {
                // A closed select paints none of its options; they are what it offers.
                let offered = 0;
                for (const option of el.options) {
                  if (offered >= maxSelectOptions) break;
                  if (!isOfferedOption(option)) continue;
                  offered += 1;
                  const before = node.children.length;
                  walk(option, node.children, isHidden, isAriaHidden);
                  if (node.children.length > before) node.children[before].hidden = isHidden;
                }
                return;
              }
              if (isLeaf(el, role) && !holdsForced(el)) return;
              walkChildren(el, node.children, childrenHidden, isAriaHidden);
              return;
            }
            walkChildren(el, into, childrenHidden, isAriaHidden);
          };
          // Light children first, then the shadow tree. Slotted elements are
          // light children and the shadow tree holds only their slots, so
          // nothing is listed twice.
          const walkChildren = (el, into, parentHidden, parentAria) => {
            for (const child of el.children) walk(child, into, parentHidden, parentAria);
            const shadow = shadowOf(el);
            if (shadow) for (const child of shadow.children) walk(child, into, parentHidden, parentAria);
          };
          // innerText applies CSS text-transform, but locators match the DOM
          // text, as Playwright's do. The walk runs with text-transform
          // turned off, so an uppercase-styled "Net worth" reads as written.
          const roots = [];
          const sheets = document.adoptedStyleSheets;
          const plain = new CSSStyleSheet();
          plain.replaceSync("* { text-transform: none !important; }");
          document.adoptedStyleSheets = [...sheets, plain];
          let truncated;
          try {
            const body = document.documentElement.inert ? null : document.body;
            truncated = !!body && tally(body, false) > max;
            if (truncated) offBudget = Math.max(0, max - tally(body, true));
            // An element outside the body (html, head, and what is in it) is walked from the root, down the paths that lead to one.
            if (body && forced !== null && [...forced].some((el) => !body.contains(el))) {
              gate = (el) => el === body || body.contains(el) || forced.has(el) || holders.has(el);
              walk(document.documentElement, roots, false, false);
            } else if (body) walk(body, roots, false, false);
          } finally {
            document.adoptedStyleSheets = sheets;
          }
          // A read for the trace leaves the elements the test and the model act on as the last real observation set them.
          if (publish) window[Symbol.for("e2e.observation.elements")] = elements;
          // The object itself, not JSON.stringify of it: the application may have replaced that, or added a toJSON.
          const result = { next, count, truncated: truncated || full || textCut, scroll, viewport: { width: innerWidth, height: innerHeight }, roots };
          // The elements the walk read, in one array beside their ids (upstream's `elements` and `ids`): the host keeps the array behind
          // one handle and makes an element handle only when an action reaches for a node.
          if (publish) {
            result.ids = [...elements.keys()];
            result.elements = [...elements.values()];
          }
          return result;
        }
        """;

    // Resolves after the next rendered frame, or at once in a hidden document (upstream's nextRenderedFrame).
    public const string NextRenderedFrame = """
        () => {
          if (document.visibilityState === "hidden") return Promise.resolve();
          return new Promise((resolve) => requestAnimationFrame(() => window.setTimeout(resolve, 0)));
        }
        """;

    // Where a wheel reaches an element in a top-level document, and the box it read when it was not blocked (upstream's wheelAim).
    public const string WheelAim = """
        (element) => {
          if (window !== window.top || element.getClientRects().length === 0) return { aim: "blocked" };
          const rect = element.getBoundingClientRect();
          const box = { x: rect.x, y: rect.y, width: rect.width, height: rect.height };
          if (element.matches(":hover")) return { aim: "pointer", box };
          const hit = document.elementFromPoint(rect.x + rect.width / 2, rect.y + rect.height / 2);
          return hit !== null && element.contains(hit) ? { aim: "centre", box } : { aim: "blocked" };
        }
        """;

    // Which kind of radio a checkable element is, if any (upstream's radioKind). A native radio can be selected but never cleared by a click.
    // An ARIA radio is clicked like any control, as Playwright does: its script decides what a click does.
    public const string RadioKind = """
        (node) => {
          if (node instanceof HTMLInputElement && node.type === "radio") return "native";
          return node.getAttribute("role") === "radio" ? "aria" : null;
        }
        """;

    // Whether an element is a file input, or labels one, so setInputFiles can take it directly (upstream's isFileInput).
    public const string IsFileInput = """
        (node) => {
          const control = node instanceof HTMLInputElement ? node : node.closest("label")?.control;
          return control instanceof HTMLInputElement && control.type === "file";
        }
        """;

    // Whether the focused element of this document, followed into shadow roots, takes keystrokes (upstream's requireEditableFocus): a text
    // field, a contenteditable host, or any other focusable element the app made focusable. The body, a button, a link, a select, a
    // frame, and a non-text input discard them.
    public const string HasEditableFocus = """
        () => {
          const roots = globalThis[Symbol.for("e2e.closedShadowRoots")];
          let active = document.activeElement;
          while (active !== null) {
            const root = active.shadowRoot ?? roots?.get(active);
            const leaf = root?.activeElement;
            if (leaf === undefined || leaf === null) break;
            active = leaf;
          }
          if (active === null || active === document.body || active === document.documentElement) return false;
          if (active instanceof HTMLInputElement) {
            return !active.disabled && !active.readOnly && !["button", "submit", "reset", "checkbox", "radio", "file", "image", "range", "color"].includes(active.type);
          }
          if (active instanceof HTMLTextAreaElement) return !active.disabled && !active.readOnly;
          if (active instanceof HTMLSelectElement || active instanceof HTMLButtonElement || active instanceof HTMLAnchorElement ||
              active instanceof HTMLIFrameElement || active instanceof HTMLFrameElement) {
            return false;
          }
          return true;
        }
        """;

    // The ref an iframe element carries, so the host can hang that frame's
    // observation under the iframe node.
    public const string RefOf = """
        (el) => el[Symbol.for("e2e.observation.ref")] ?? null
        """;

    // What a locator's selector names, in one frame: per element, its ref when the last observation lists it, else null. Those are marked,
    // so the observation that follows lists them as nodes (the page-side half of upstream's e2e-read engine).
    public const string RefsOrForce = """
        (els, mark) => {
          const listed = window[Symbol.for("e2e.observation.elements")];
          const key = Symbol.for("e2e.observation.forced");
          return els.map((el) => {
            const id = el[Symbol.for("e2e.observation.ref")];
            if (id !== undefined && listed?.get(id) === el && el.isConnected) return id;
            if (!mark) return null;
            const forced = globalThis[key] ?? new Set();
            forced.add(el);
            globalThis[key] = forced;
            return null;
          });
        }
        """;

    // Whether each element is a frame, the only element a frame locator can enter. An element gone since the count lists nothing.
    public const string AreFrameElements = """
        (els) => els.map((el) => el.tagName === "IFRAME" || el.tagName === "FRAME")
        """;

    // Where an iframe element's content box, the box its document's viewport
    // fills, sits inside its border box: past the border and the padding.
    public const string FrameInset = """
        (el) => {
          const style = getComputedStyle(el);
          const left = Number.parseFloat(style.paddingLeft) || 0;
          const top = Number.parseFloat(style.paddingTop) || 0;
          const right = Number.parseFloat(style.paddingRight) || 0;
          const bottom = Number.parseFloat(style.paddingBottom) || 0;
          return {
            x: el.clientLeft + left,
            y: el.clientTop + top,
            width: Math.max(0, el.clientWidth - left - right),
            height: Math.max(0, el.clientHeight - top - bottom),
          };
        }
        """;

    // Context init script that keeps closed shadow roots reachable for
    // Collect. attachShadow is the one way a script makes such a root;
    // declarative closed roots are parsed, not attached, and stay out of reach.
    // The record is a WeakMap from host to root with one own property beside the
    // map: count, the number of closed roots the document has attached. It lets
    // the mask engine answer for a document without closed roots without walking it.
    public const string RecordClosedShadowRoots = """
        (() => {
          const key = Symbol.for("e2e.closedShadowRoots");
          if (Object.prototype.hasOwnProperty.call(globalThis, key)) return;
          const roots = new WeakMap();
          Object.defineProperty(roots, "count", { value: 0, enumerable: false, configurable: false, writable: true });
          Object.defineProperty(globalThis, key, { value: roots, enumerable: false, configurable: false, writable: false });
          const attachShadow = Element.prototype.attachShadow;
          Element.prototype.attachShadow = function (init) {
            const root = attachShadow.call(this, init);
            if (root.mode === "closed") {
              roots.set(this, root);
              roots.count += 1;
            }
            return root;
          };
        })();
        """;

    // Playwright selector engine name; e2e-closed=<css> matches inside recorded closed shadow roots.
    public const string ClosedShadowSelectorEngine = "e2e-closed";

    // The engine behind e2e-closed=<css>: every element matching the CSS selector inside a closed shadow root
    // the init script recorded, reached from the query root through light DOM and open roots, and through roots
    // of either kind nested below a closed one. Playwright's own selectors stop at a closed root, so this is what
    // lets a screenshot mask cover a secure field there. A closed root is searched only as an entry of the first
    // list, never by recursion from the tree above it, so each element is reached from exactly one root. A document
    // that has attached no closed root has none to find, and the walk is skipped. It runs in the page's main world
    // (not as a content script) because that is where the record lives. A document without the record is refused:
    // a mask that cannot prove its coverage must fail closed. The record counts as present when the init script
    // defined it on the global, checked with in because it calls nothing the page can replace: instanceof WeakMap
    // reads the page's own WeakMap, which a polyfill may swap out.
    public const string ClosedShadowSelectorEngineSource = """
        () => {
          const key = Symbol.for("e2e.closedShadowRoots");
          const tracked = key in globalThis;
          const roots = globalThis[key];
          const closedRootsUnder = (root, out) => {
            if (!tracked || roots.count === 0) return out;
            const scan = (el) => {
              const closed = roots.get(el);
              if (closed !== undefined) out.push(closed);
              const nested = el.shadowRoot ?? closed;
              if (nested !== undefined && nested !== null) closedRootsUnder(nested, out);
            };
            if (root.nodeType === 1) scan(root);
            for (const el of root.querySelectorAll("*")) scan(el);
            return out;
          };
          const matchesIn = (root, selector, out) => {
            for (const el of root.querySelectorAll(selector)) out.push(el);
            if (root.nodeType === 1 && root.shadowRoot !== null) matchesIn(root.shadowRoot, selector, out);
            for (const el of root.querySelectorAll("*")) {
              if (el.shadowRoot !== null) matchesIn(el.shadowRoot, selector, out);
            }
            return out;
          };
          const queryAll = (root, selector) => {
            if (!tracked) throw new Error("closed-shadow root tracking is unavailable for this document");
            const out = [];
            for (const closed of closedRootsUnder(root, [])) matchesIn(closed, selector, out);
            return out;
          };
          return { queryAll, query: (root, selector) => queryAll(root, selector)[0] ?? null };
        }
        """;

    // Whether a document ran RecordClosedShadowRoots.
    public const string TracksClosedShadowRoots = """
        () => Symbol.for("e2e.closedShadowRoots") in globalThis
        """;
}
