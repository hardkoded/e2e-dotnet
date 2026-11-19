// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli.Tests.Yaml;

/// <summary>.NET-only: <see cref="YamlDocument"/> stands for the <c>yaml</c> npm library upstream edits its CI templates with, so no upstream <c>it</c> covers it.</summary>
public sealed class YamlDocumentTests
{
    private const string Text = """
        # Header

        # About jobs
        jobs:
          web:
            steps:
              - uses: a/b@c # v1
              # Install
              - name: Install
                run: |
                  one
                  two
                with: [a, b]

          ios:
            tags: [macos]
        """;

    [Fact]
    public void Prints_a_document_back_as_it_was_read()
    {
        Assert.Equal(Text + "\n", YamlDocument.Parse(Text + "\n").ToString());
    }

    [Fact]
    public void Keeps_comments_and_blank_lines_when_a_node_changes_or_goes()
    {
        var doc = YamlDocument.Parse(Text);
        var jobs = ((YamlMap)doc.Contents!).Get<YamlMap>("jobs");
        var steps = jobs.Get<YamlMap>("web").Get<YamlSeq>("steps");
        ((YamlMap)steps.Items[1]).Set("run", "echo hi");
        jobs.Delete("ios");
        Assert.Equal(
            "# Header\n\n# About jobs\njobs:\n  web:\n    steps:\n      - uses: a/b@c # v1\n      # Install\n      - name: Install\n        run: echo hi\n        with: [a, b]\n",
            doc.ToString()
        );
    }

    [Fact]
    public void Drops_the_blank_line_above_the_first_key_and_replaces_a_comment()
    {
        var doc = YamlDocument.Parse(Text);
        var jobs = ((YamlMap)doc.Contents!).Get<YamlMap>("jobs");
        jobs.Delete("web");
        jobs.Items[0].Key.SpaceBefore = false;
        jobs.Items[0].Key.CommentBefore = "Phones";
        Assert.Contains("jobs:\n  # Phones\n  ios:\n", doc.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Writes_a_new_value_plain_when_it_reads_back_the_same_else_quoted_or_as_a_block()
    {
        var map = YamlMap.Of([
            new("plain", "npm ci"),
            new("colon", "a: b"),
            new("word", "true"),
            new("block", "a\nb\n"),
            new("list", new[] { "x", "z" }),
        ]);
        var doc = YamlDocument.Parse("k: v\n");
        ((YamlMap)doc.Contents!).Set("map", map);
        Assert.Equal(
            "k: v\nmap:\n  plain: npm ci\n  colon: \"a: b\"\n  word: \"true\"\n  block: |\n    a\n    b\n  list:\n    - x\n    - z\n",
            doc.ToString()
        );
    }
}
