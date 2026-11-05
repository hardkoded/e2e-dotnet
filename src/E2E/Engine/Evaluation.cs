// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Engine;

/// <summary>Separates exceptions thrown by trusted page code from failures of the evaluation transport (upstream's <c>evaluation.ts</c>).</summary>
internal static class Evaluation
{
    /// <summary>
    /// The source of a function that builds, in the page, the encoder of an evaluation result into JSON text (upstream's
    /// <c>pageValueEncoder</c>). Called before the caller's code runs, it takes the intrinsics it needs from literals, so code that
    /// deletes or replaces <c>Array</c>, <c>Object</c>, or <c>JSON</c> cannot reach it. Only JSON passes: <c>null</c>, booleans, finite
    /// numbers, strings, arrays, and plain objects. Anything else throws <c>{ invalid }</c> with the reason.
    /// </summary>
    public const string PageValueEncoder = """
        () => {
          const { getPrototypeOf, keys } = ({}).constructor;
          const objectPrototype = getPrototypeOf({});
          const { isArray } = [].constructor;
          const charCodeAt = ''.charCodeAt;
          const hex = '0123456789abcdef';
          const invalid = (reason) => ({ invalid: reason });
          const quote = (text) => {
            let out = '"';
            for (let i = 0; i < text.length; i++) {
              const code = charCodeAt.call(text, i);
              const escape = code === 34 || code === 92 || code < 32 || code === 0x2028 || code === 0x2029 || (code >= 0xd800 && code <= 0xdfff);
              out += escape ? `\\u${hex[code >> 12]}${hex[(code >> 8) & 15]}${hex[(code >> 4) & 15]}${hex[code & 15]}` : text[i];
            }
            return `${out}"`;
          };
          const ancestors = [];
          const encode = (value) => {
            if (value === null) return 'null';
            switch (typeof value) {
              case 'string':
                return quote(value);
              case 'boolean':
                return value ? 'true' : 'false';
              case 'number':
                if (value !== value || value === Infinity || value === -Infinity) throw invalid('contains a non-finite number');
                return `${value}`;
              case 'object':
                break;
              default:
                throw invalid(`must be JSON-safe, found ${typeof value}`);
            }
            const array = isArray(value);
            const prototype = getPrototypeOf(value);
            if (!array && prototype !== objectPrototype && prototype !== null) throw invalid('must be JSON-safe');
            for (let i = 0; i < ancestors.length; i++) {
              if (ancestors[i] === value) throw invalid('contains a cycle');
            }
            ancestors[ancestors.length] = value;
            let text = '';
            if (array) {
              for (let i = 0; i < value.length; i++) text += `${i === 0 ? '' : ','}${encode(value[i])}`;
              text = `[${text}]`;
            } else {
              const names = keys(value);
              for (let i = 0; i < names.length; i++) text += `${i === 0 ? '' : ','}${quote(names[i])}:${encode(value[names[i]])}`;
              text = `{${text}}`;
            }
            ancestors.length -= 1;
            return text;
          };
          return (root) => (root === undefined ? undefined : encode(root));
        }
        """;

    /// <summary>
    /// Compiles the page-side error boundary around one expression: a function it evaluates to is called with the
    /// argument, any other value is the result. The page code runs inside a try, so an exception it throws comes
    /// back as data, apart from the browser's own failures. The boundary resolves to JSON text, a primitive the caller takes
    /// back without Playwright serializing it in the page, so a page that deleted or replaced a global the serializer needs still
    /// returns its result; <see cref="DecodeEvaluation"/> reads it.
    /// </summary>
    public static string CompileEvaluation(string expression, bool hasArg) => $$"""
        (arg) => {
          return (async (encode, run) => {
            const report = (ok, key, text) => '{"ok":' + ok + (text === undefined ? '' : ',"' + key + '":' + text) + '}';
            let value;
            try {
              value = await run();
            } catch (cause) {
              let message;
              try {
                message = typeof cause?.message === 'string' ? cause.message : `${cause}`;
              } catch {
                message = 'Page evaluation threw an unprintable value';
              }
              return report(false, 'message', encode(message));
            }
            try {
              return report(true, 'value', encode(value));
            } catch (cause) {
              if (cause !== null && typeof cause === 'object' && typeof cause.invalid === 'string') {
                return report(false, 'invalid', encode(cause.invalid));
              }
              return '{"ok":false,"message":"evaluate result cannot be read"}';
            }
          })(({{PageValueEncoder}})(), async () => ((result) => typeof result === 'function' ? result({{(hasArg ? "arg" : "")}}) : result)(({{expression}}
          )));
        }
        """;

    /// <summary>The result of the boundary <see cref="CompileEvaluation"/> compiled, from the JSON text it resolved to; throws what the page threw.</summary>
    public static JsonElement? DecodeEvaluation(string? text)
    {
        JsonDocument report;
        try
        {
            report = JsonDocument.Parse(text ?? "");
        }
        catch (JsonException ex)
        {
            throw new TestException("EVALUATE_FAILED", "page evaluation returned an unreadable result", ex);
        }

        using (report)
        {
            var root = report.RootElement;
            if (root.GetProperty("ok").GetBoolean())
            {
                return root.TryGetProperty("value", out var value) ? value.Clone() : null;
            }

            if (root.TryGetProperty("invalid", out var invalid))
            {
                throw new TestException("INVALID_ARGUMENT", "evaluate result " + invalid.GetString());
            }

            throw new TestException("EVALUATE_FAILED", root.GetProperty("message").GetString()!);
        }
    }
}
