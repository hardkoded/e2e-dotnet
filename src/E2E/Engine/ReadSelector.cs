// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Encodings.Web;
using System.Text.Json;

namespace E2E.Engine;

/// <summary>
/// The selector engine that reads a locate's matches in the task that finds them (upstream's <c>web/src/read-selector.ts</c>). Appended
/// to a projected locator (<c>&lt;locator&gt; &gt;&gt; e2e-read=&lt;json&gt;</c>), it maps each match to itself and records the reader's
/// semantics for it on the element, so a page that replaces the match a frame later can never detach it between the query and the read:
/// both happen before the page runs again. A later protocol call takes those reads back from the very elements the query returned, as
/// values or as handles to pin.
/// </summary>
internal static class ReadSelector
{
    /// <summary>Playwright selector engine name; <c>e2e-read=&lt;json&gt;</c> reads every match under one locate's token (upstream's <c>READ_SELECTOR_ENGINE</c>).</summary>
    public const string ReadSelectorEngine = "e2e-read";

    // As JSON.stringify writes it: a quote or a backslash escaped, no other character (the default encoder also escapes < > & ' +).
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// The selector one locate appends to its projected locator, under a fresh token so concurrent locates of one element each take back
    /// their own read (upstream's <c>readSelector</c>). The body is JSON, kept whole by Playwright's selector parser like the
    /// <c>e2e-label</c> body.
    /// </summary>
    public static (string Selector, string Token) Create(string testIdAttribute, string secureFieldSelector)
    {
        var token = Guid.NewGuid().ToString();
        return (ReadSelectorEngine + "=" + JsonSerializer.Serialize(new ReadSelectorBody(token, testIdAttribute, secureFieldSelector), Json), token);
    }

    // Where the engine records reads on an element: a Map from locate token to read under a registry symbol, like the observation's ref
    // stamp: no attribute, nothing a key listing shows, not removable by application code, and gone with the element. The take function
    // below repeats the literal, since a page function cannot import it (upstream's READS_KEY).
    private const string ReadsKey = "e2e.locate.reads";

    /// <summary>
    /// The engine behind <c>e2e-read=&lt;json&gt;</c>: each query root is a match of the locator before it, returned as is after the reader's
    /// node read of it is recorded under the body's token. The reader is embedded as source because a selector engine is one self-contained
    /// page function, and built for each query, as upstream calls the reader once per read, so its memo caches never outlive a query. It
    /// runs in the main world (not as a content script) so the take function, which Playwright evaluates there, finds what it recorded.
    /// Upstream's <c>READ_SELECTOR_ENGINE_SOURCE</c>.
    /// </summary>
    public static readonly string ReadSelectorEngineSource = $$"""
        () => {
          const readerOf = () => {
        {{ReadSemantics.Prelude}}
        {{ReadSemantics.NodeMode}}
            return nodeOf;
          };
          const key = Symbol.for({{JsonSerializer.Serialize(ReadsKey)}});
          const queryAll = (root, selector) => {
            if (root.nodeType !== 1) return [];
            const body = JSON.parse(selector);
            const read = readerOf()(root, body.testIdAttribute);
            if (!Object.prototype.hasOwnProperty.call(root, key)) {
              Object.defineProperty(root, key, { value: new Map(), enumerable: false, configurable: false, writable: false });
            }
            root[key].set(body.token, read);
            return [root];
          };
          return { queryAll, query: (root, selector) => queryAll(root, selector)[0] ?? null };
        }
        """;

    /// <summary>
    /// Takes back, in order, the reads the engine recorded under <c>token</c> and deletes them; null where none was recorded, which is a
    /// match the engine never saw: a <c>*</c> capture earlier in the selector returns an element the chain only passed through.
    /// <c>locator.evaluateAll</c> calls it with the matched elements. A caller pinning handles evaluates it on the first handle with every
    /// handle in <c>elements</c>, so it runs in the frame the handles belong to; <c>page.evaluate</c> would reject handles taken inside an
    /// iframe. Upstream's <c>takeReadsFunction</c>.
    /// </summary>
    public const string TakeReadsFunction = """
        (subject, arg) =>
          (Array.isArray(subject) ? subject : (arg.elements ?? [])).map((element) => {
            const reads = element[Symbol.for("e2e.locate.reads")];
            const read = reads?.get(arg.token) ?? null;
            reads?.delete(arg.token);
            return read;
          })
        """;

    // What the e2e-read selector body carries into the page: the locate's token and the reader's options (upstream's ReadSelectorBody).
    private sealed record ReadSelectorBody(string Token, string TestIdAttribute, string SecureFieldSelector);
}
