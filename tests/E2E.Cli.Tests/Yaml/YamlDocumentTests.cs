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

    [Fact]
    public void Keeps_a_comment_after_a_block_key_a_quoted_hash_in_a_flow_list_and_trailing_spaces_in_a_block()
    {
        const string Source = "jobs: # all\n  a:\n    tags: [\"qa #1\"] # runner\n    run: |\n      echo  \n";
        Assert.Equal(Source, YamlDocument.Parse(Source).ToString());
    }

    [Fact]
    public void Keeps_the_comment_after_a_scalar_that_takes_a_scalars_place()
    {
        var doc = YamlDocument.Parse("run: old # why\n");
        ((YamlMap)doc.Contents!).Set("run", "new");
        Assert.Equal("run: new # why\n", doc.ToString());
    }

    [Fact]
    public void Quotes_text_a_CI_service_reads_as_a_number_or_null_and_keeps_every_trailing_newline()
    {
        var doc = YamlDocument.Parse("k: v\n");
        var map = (YamlMap)doc.Contents!;
        map.Set("hex", "0x2a");
        map.Set("two", "a\n\n");
        Assert.Equal("k: v\nhex: \"0x2a\"\ntwo: \"a\\n\\n\"\n", doc.ToString());
    }
}
