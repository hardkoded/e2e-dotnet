// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;

namespace E2E.Cli.Mcp;

/// <summary>
/// Where a test is declared, for <c>open_session {test: "file:line"}</c> (.NET-only: upstream reads the line from the test file it collects).
/// The line comes from the assembly's portable PDB and the source it names: a test is declared on the lines from its first attribute to its
/// signature, and a line inside its body names no test.
/// </summary>
internal static partial class TestLocations
{
    /// <summary>The lines a test is declared on in one source file.</summary>
    internal sealed record Span(int First, int Last)
    {
        public bool Contains(int line) => line >= First && line <= Last;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<MethodInfo, Dictionary<string, Span>> Known = new();

    [GeneratedRegex(@"^(?<file>.+\.[A-Za-z]+):(?<line>[0-9]+)\z", RegexOptions.CultureInvariant)]
    private static partial Regex FileLine();

    /// <summary>Whether <paramref name="name"/> reads as <c>file:line</c>.</summary>
    public static bool TryParse(string name, out string file, out int line)
    {
        var match = FileLine().Match(name);
        file = match.Success ? match.Groups["file"].Value : string.Empty;
        line = 0;
        return match.Success && int.TryParse(match.Groups["line"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out line);
    }

    /// <summary>The lines <paramref name="test"/> is declared on in the source file <paramref name="file"/> names, or null when it is declared elsewhere.</summary>
    public static Span? Declared(SessionTestInfo test, string file)
    {
        if (test.Method is not { } method)
        {
            return null;
        }

        var wanted = Normalize(file);
        foreach (var (document, span) in Spans(method))
        {
            var path = Normalize(document);
            if (path == wanted || path.EndsWith("/" + wanted.TrimStart('/'), StringComparison.OrdinalIgnoreCase))
            {
                return span;
            }
        }

        return null;
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static Dictionary<string, Span> Spans(MethodInfo method) => Known.GetValue(method, Read);

    private static Dictionary<string, Span> Read(MethodInfo method)
    {
        Dictionary<string, Span> spans = new(StringComparer.Ordinal);
        if (SessionTests.PathOf(method.Module.Assembly) is not { } dllPath)
        {
            return spans;
        }

        using var stream = File.OpenRead(dllPath);
        using var pe = new PEReader(stream);
        MetadataReaderProvider? embedded = null;
        MetadataReaderProvider? provider = null;
        try
        {
            if (!pe.TryOpenAssociatedPortablePdb(dllPath, path => File.Exists(path) ? new MemoryStream(File.ReadAllBytes(path)) : null, out provider, out _))
            {
                return spans;
            }

            var reader = provider!.GetMetadataReader();
            var points = PointsOf(reader, method.MetadataToken);

            // An async or iterator method's body is its state machine's MoveNext.
            if (points.IsEmpty && method.GetCustomAttributes<System.Runtime.CompilerServices.StateMachineAttribute>().FirstOrDefault()?.StateMachineType.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) is { } moveNext)
            {
                points = PointsOf(reader, moveNext.MetadataToken);
            }

            foreach (var group in points.GroupBy(point => point.Document))
            {
                var document = reader.GetString(reader.GetDocument(group.Key).Name);
                var first = group.Min(point => point.StartLine);
                spans[document] = Extend(document, method.Name, first);
            }
        }
#pragma warning disable CA1031 // A test assembly without readable debug information has no declared lines: it is named by its full name.
        catch (Exception cause) when (cause is BadImageFormatException or IOException or InvalidOperationException)
#pragma warning restore CA1031
        {
            spans.Clear();
        }
        finally
        {
            provider?.Dispose();
            embedded?.Dispose();
        }

        return spans;
    }

    private static ImmutableArray<SequencePoint> PointsOf(MetadataReader reader, int token) =>
        [.. reader.GetMethodDebugInformation(MetadataTokens.MethodDefinitionHandle(token).ToDebugInformationHandle()).GetSequencePoints().Where(point => !point.IsHidden)];

    // The first sequence point is the body's opening brace or expression: the signature is the line of the method's name above it, and the
    // attributes sit on the lines above that.
    private static Span Extend(string document, string methodName, int bodyLine)
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(document);
        }
        catch (Exception cause) when (cause is IOException or UnauthorizedAccessException)
        {
            return new Span(bodyLine, bodyLine);
        }

        var name = Math.Min(bodyLine, lines.Length);
        while (name > 1 && !lines[name - 1].Contains(methodName + "(", StringComparison.Ordinal) && bodyLine - name < 12)
        {
            name--;
        }

        if (!lines[Math.Max(name, 1) - 1].Contains(methodName + "(", StringComparison.Ordinal))
        {
            return new Span(bodyLine, bodyLine);
        }

        var first = name;
        while (first > 1 && lines[first - 2].TrimStart().StartsWith('['))
        {
            first--;
        }

        // A brace on a line of its own closes the signature: that line names the test too.
        var last = bodyLine <= lines.Length && lines[bodyLine - 1].Trim() == "{" ? bodyLine : name;
        return new Span(first, Math.Max(last, name));
    }
}
