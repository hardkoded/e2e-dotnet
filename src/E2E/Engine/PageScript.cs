// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Engine;

internal static class PageScript
{
    // Each element keeps its ref under a registry symbol for the life of the
    // document, so a ref the model saw stays valid in the next observation.
    // The host passes the next free number, so refs never repeat across
    // documents. The latest observation's elements are kept for Find. The
    // walk reads one document: it goes through open shadow roots and closed
    // ones the init script recorded, and lists an iframe as a boundary node
    // the host fills with that frame's own observation. It stops at the node
    // budget and says so.
    public const string Collect = """
        ({ seed, max, testIdAttribute }) => {
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
          const skip = new Set(["SCRIPT", "STYLE", "NOSCRIPT", "TEMPLATE"]);
          const leaves = new Set(["button", "link", "textbox", "checkbox", "radio", "searchbox", "heading", "status", "image", "tab",
            "option", "menuitem", "menuitemcheckbox", "menuitemradio", "switch", "slider", "spinbutton", "progressbar", "meter", "separator", "iframe"]);
          const maxSelectOptions = 60;
          let count = 0;
          let truncated = false;
          const closedRoots = globalThis[Symbol.for("e2e.closedShadowRoots")];
          const shadowOf = (el) => el.shadowRoot ?? closedRoots?.get(el) ?? null;
          let focused = document.activeElement;
          while (focused) {
            const inner = shadowOf(focused)?.activeElement ?? null;
            if (!inner) break;
            focused = inner;
          }
          const hasOwnName = (el) => ["aria-label", "aria-labelledby", "title"].some((attribute) => (el.getAttribute(attribute) || "").trim() !== "");
          const pageLevel = (el) => el.closest("article, aside, main, nav, section, [role~=article], [role~=complementary], [role~=main], [role~=navigation], [role~=region]") === null;
          const cut = (value, limit) => {
            const text = (value || "").replace(/\s+/g, " ").trim();
            return text.length > limit ? text.slice(0, limit) : text;
          };
          const hidden = (el) => {
            if (el.hasAttribute("hidden") || el.getAttribute("aria-hidden") === "true") return true;
            const style = getComputedStyle(el);
            return style.display === "none" || style.visibility === "hidden";
          };
          const roleOf = (el) => {
            const explicit = (el.getAttribute("role") || "").trim();
            if (explicit) {
              const first = explicit.split(/\s+/)[0];
              return first === "img" ? "image" : first;
            }
            const tag = el.tagName;
            switch (tag) {
              case "A": return el.hasAttribute("href") ? "link" : null;
              case "BUTTON": return "button";
              case "SELECT": return el.multiple || el.size > 1 ? "listbox" : "combobox";
              case "TEXTAREA": return "textbox";
              case "IMG": return el.getAttribute("alt") === "" ? "presentation" : "image";
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
                const tableRole = (el.closest("table")?.getAttribute("role") || "").trim().split(/\s+/)[0];
                return tableRole === "grid" || tableRole === "treegrid" ? "gridcell" : "cell";
              }
              case "TH": {
                const scope = (el.getAttribute("scope") || "").toLowerCase();
                return scope === "row" || scope === "rowgroup" ? "rowheader" : "columnheader";
              }
              case "DIALOG": return "dialog";
              case "OUTPUT": return "status";
              case "P": return "paragraph";
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
          // accname reads an aria-labelledby reference (2B) before the
          // element's own aria-label (2C), then its associated labels.
          const nameOf = (el, role) => {
            const labelledby = (el.getAttribute("aria-labelledby") || "").trim();
            if (labelledby) {
              const text = labelledby.split(/\s+/).map((id) => document.getElementById(id)?.innerText || "").join(" ");
              if (text.trim()) return cut(text, 256);
            }
            const aria = el.getAttribute("aria-label");
            if (aria && aria.trim()) return cut(aria, 256);
            if (el.labels && el.labels.length) return cut(el.labels[0].innerText || "", 256);
            if (role === "textbox" || role === "searchbox") return cut(el.getAttribute("placeholder") || "", 256);
            if (role) return cut(el.innerText || el.getAttribute("alt") || "", 256);
            return "";
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
          const walk = (el, into) => {
            if (!el || skip.has(el.tagName) || truncated) return;
            const role = roleOf(el);
            const testId = el.getAttribute(testIdAttribute);
            const isFrame = el.tagName === "IFRAME";
            if (isFrame && hidden(el)) return;
            if ((role && role !== "presentation" && role !== "none") || testId) {
              if (count >= max) {
                truncated = true;
                return;
              }
              count++;
              const type = (el.getAttribute("type") || "").toLowerCase();
              const secure = type === "password" || el.getAttribute("autocomplete") === "current-password";
              const node = {
                ref: stamp(el),
                role,
                name: isFrame ? cut(el.getAttribute("title") || "", 256) : nameOf(el, role),
                text: role && leaves.has(role) ? null : cut(el.innerText || "", 512),
                value: secure || !("value" in el) || el.tagName === "OPTION" ? null : String(el.value ?? ""),
                testId,
                placeholder: el.getAttribute("placeholder"),
                inputPurpose: secure ? "password" : null,
                level: /^H[1-6]$/.test(el.tagName) ? Number(el.tagName.slice(1)) : null,
                disabled: disabledOf(el, role),
                checked: checkedOf(el),
                expanded: el.getAttribute("aria-expanded") === "true",
                selected: el.tagName === "OPTION" ? !!el.selected : el.getAttribute("aria-selected") === "true",
                pressed: el.getAttribute("aria-pressed") === "true",
                focused: el === focused,
                hidden: hidden(el),
                secure,
                frame: isFrame,
                children: []
              };
              into.push(node);
              if (el.tagName === "SELECT") {
                // A closed select paints none of its options; they are what it offers.
                for (const option of Array.from(el.options).slice(0, maxSelectOptions)) {
                  const before = node.children.length;
                  walk(option, node.children);
                  if (node.children.length > before) node.children[before].hidden = false;
                }
                return;
              }
              if (role && leaves.has(role)) return;
              walkChildren(el, node.children);
              return;
            }
            walkChildren(el, into);
          };
          // Light children first, then the shadow tree. Slotted elements are
          // light children and the shadow tree holds only their slots, so
          // nothing is listed twice.
          const walkChildren = (el, into) => {
            for (const child of el.children) walk(child, into);
            const shadow = shadowOf(el);
            if (shadow) for (const child of shadow.children) walk(child, into);
          };
          const roots = [];
          if (document.body) walk(document.body, roots);
          window[Symbol.for("e2e.observation.elements")] = elements;
          return JSON.stringify({ next, count, truncated, roots });
        }
        """;

    // The ref an iframe element carries, so the host can hang that frame's
    // observation under the iframe node.
    public const string RefOf = """
        (el) => el[Symbol.for("e2e.observation.ref")] ?? null
        """;

    // Context init script that keeps closed shadow roots reachable for
    // Collect. attachShadow is the one way a script makes such a root;
    // declarative closed roots are parsed, not attached, and stay out of reach.
    public const string RecordClosedShadowRoots = """
        (() => {
          const key = Symbol.for("e2e.closedShadowRoots");
          if (Object.prototype.hasOwnProperty.call(globalThis, key)) return;
          const roots = new WeakMap();
          Object.defineProperty(globalThis, key, { value: roots, enumerable: false, configurable: false, writable: false });
          const attachShadow = Element.prototype.attachShadow;
          Element.prototype.attachShadow = function (init) {
            const root = attachShadow.call(this, init);
            if (root.mode === "closed") roots.set(this, root);
            return root;
          };
        })();
        """;

    public const string Find = """
        (id) => {
          const el = window[Symbol.for("e2e.observation.elements")]?.get(id);
          return el && el.isConnected ? el : null;
        }
        """;
}
