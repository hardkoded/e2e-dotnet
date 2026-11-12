// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace E2E.Cli;

/// <summary>
/// The test project <c>e2e init</c> reads: the document, and the text it was read from so an edit keeps its formatting
/// (upstream's <c>readPackage</c> result, whose <c>manifest</c> is a <c>.csproj</c> here).
/// </summary>
/// <param name="FileName">The project file's name when it exists; null for one init is about to create.</param>
/// <param name="Original">The text read, or null when there was none.</param>
/// <param name="Document">The project document.</param>
internal sealed record PackageProject(string? FileName, string? Original, XDocument Document);

/// <summary>
/// Upstream's <c>cli/init/package.ts</c>, with the <c>.csproj</c> in place of <c>package.json</c>: reading it and adding
/// dependencies. There is no scripts section, so <c>addScripts</c> has no counterpart.
/// </summary>
internal static class Package
{
    /// <summary>The XML of a project upstream would start as a private ESM package: a test project of the framework.</summary>
    private static string NewProject(FrameworkPreset framework) => $"""
        <Project Sdk="Microsoft.NET.Sdk">

          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
            <IsPackable>false</IsPackable>
            <IsTestProject>true</IsTestProject>{(framework.Executable ? "\n    <OutputType>Exe</OutputType>" : "")}
          </PropertyGroup>

        </Project>

        """;

    /// <summary>
    /// Reads and validates the project in <paramref name="cwd"/>, keeping its original text so edits can preserve formatting.
    /// A missing project starts as a test project of <paramref name="framework"/>. Throws <see cref="InvalidOperationException"/>
    /// with the reason when a file cannot be read, or when more than one project file leaves the choice open.
    /// </summary>
    public static PackageProject ReadPackage(string cwd, FrameworkPreset framework)
    {
        var projects = Directory.Exists(cwd) ? Directory.GetFiles(cwd, "*.csproj") : [];
        if (projects.Length > 1)
        {
            throw new InvalidOperationException(cwd + " holds more than one project file (" + string.Join(", ", projects.Select(Path.GetFileName).Order(StringComparer.Ordinal)) + "); run e2e init in the directory of the test project");
        }

        var original = projects.Length == 1 ? File.ReadAllText(projects[0]) : null;
        var path = projects.Length == 1 ? projects[0] : Path.Combine(cwd, "project.csproj");
        try
        {
            var document = XDocument.Parse(original ?? NewProject(framework), LoadOptions.PreserveWhitespace);
            if (document.Root?.Name.LocalName != "Project")
            {
                throw new FormatException("the root element is not <Project>");
            }

            return new PackageProject(original is null ? null : Path.GetFileName(path), original, document);
        }
        catch (Exception cause) when (cause is XmlException or FormatException)
        {
            throw new InvalidOperationException(path + " could not be read: " + cause.Message + "; fix it before running e2e init");
        }
    }

    /// <summary>The names of the packages the project references (upstream's <c>declaresDependency</c> looks across every section).</summary>
    public static IReadOnlyList<string> DeclaredDependencies(PackageProject project) =>
        [.. project.Document.Descendants()
            .Where(element => element.Name.LocalName is "PackageReference" or "PackageVersion" or "ProjectReference")
            .Select(element => element.Name.LocalName == "ProjectReference"
                ? Path.GetFileNameWithoutExtension(((string?)element.Attribute("Include") ?? "").Replace('\\', '/'))
                : (string?)element.Attribute("Include") ?? (string?)element.Attribute("Update") ?? "")
            .Where(name => name.Length > 0)];

    /// <summary>
    /// Adds only missing dependencies, so an existing version or project link is kept, and returns what was added.
    /// A new reference goes after the last one, in its indentation; a project with none gets an item group.
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, string>> AddDependencies(PackageProject project, IEnumerable<KeyValuePair<string, string>> dependencies)
    {
        var declared = DeclaredDependencies(project);
        // Any xunit.v3 package (xunit.v3, xunit.v3.core, xunit.v3.mtp-off) is xUnit v3; adding another beside it breaks the restore.
        var additions = dependencies.Where(entry => !declared.Contains(entry.Key, StringComparer.OrdinalIgnoreCase)
            && !(entry.Key.StartsWith("xunit.v3", StringComparison.OrdinalIgnoreCase) && declared.Any(name => name.StartsWith("xunit.v3", StringComparison.OrdinalIgnoreCase)))).ToList();
        if (additions.Count == 0)
        {
            return additions;
        }

        var root = project.Document.Root!;

        // The parser reads every line break as "\n"; SerializePackage writes the original convention back.
        var last = root.Descendants().LastOrDefault(element => element.Name.LocalName == "PackageReference");
        if (last is null)
        {
            // The project's own indentation, from the first thing inside <Project>.
            var unit = IndentOf(root) is { Length: > 0 } indent ? indent : "  ";
            var group = new XElement(root.Name.Namespace + "ItemGroup");
            foreach (var (name, version) in additions)
            {
                group.Add(new XText("\n" + unit + unit), Reference(root, name, version));
            }

            group.Add(new XText("\n" + unit));

            // The group goes after a blank line, before the whitespace that precedes the closing tag.
            if (root.LastNode is XText { Value: var tail } trailing && string.IsNullOrWhiteSpace(tail))
            {
                trailing.AddBeforeSelf(new XText("\n\n" + unit), group);
            }
            else
            {
                root.Add(new XText("\n\n" + unit), group, new XText("\n"));
            }

            return additions;
        }

        var spacing = last.PreviousNode is XText { Value: var text } && string.IsNullOrWhiteSpace(text) ? text : "\n    ";
        var anchor = last;
        foreach (var (name, version) in additions)
        {
            var reference = Reference(last, name, version);
            anchor.AddAfterSelf(new XText(spacing), reference);
            anchor = reference;
        }

        return additions;
    }

    /// <summary>Serializes with the original project's newline convention and declaration.</summary>
    public static string SerializePackage(PackageProject project)
    {
        var builder = new StringBuilder();
        using (var writer = new StringWriter(builder))
        {
            using var xml = XmlWriter.Create(writer, new XmlWriterSettings { OmitXmlDeclaration = true, NewLineHandling = NewLineHandling.None });
            // The nodes before the root (a license comment) stay.
            foreach (var node in project.Document.Nodes())
            {
                node.WriteTo(xml);
            }
        }

        var body = builder.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
        var text = project.Document.Declaration is { } declaration ? declaration + "\n" + body : body;
        var newline = project.Original is not null && project.Original.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        return (text.EndsWith('\n') ? text : text + "\n").Replace("\n", newline, StringComparison.Ordinal);
    }

    private static XElement Reference(XElement sibling, string name, string version) =>
        new(sibling.Name.Namespace + "PackageReference", new XAttribute("Include", name), new XAttribute("Version", version));

    /// <summary>The whitespace that indents the first child of <paramref name="element"/>, without its line break.</summary>
    private static string IndentOf(XElement element)
    {
        var value = (element.FirstNode as XText)?.Value ?? "";
        var at = value.LastIndexOf('\n');
        return at < 0 ? "" : value[(at + 1)..];
    }
}
