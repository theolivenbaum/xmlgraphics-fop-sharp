// Licensed to the Apache Software Foundation (ASF) under one or more
// contributor license agreements.  See the NOTICE file distributed with
// this work for additional information regarding copyright ownership.
// The ASF licenses this file to You under the Apache License, Version 2.0
// (the "License"); you may not use this file except in compliance with
// the License.  You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Text;
using Fop.Fo;

namespace Fop.Render.Text;

/// <summary>
/// Renders an XSL-FO document's logical content to GitHub-flavoured Markdown: headings (by font size),
/// paragraphs with <c>**bold**</c>/<c>_italic_</c>/<c>[links](uri)</c>, bullet lists, pipe tables and
/// <c>![alt](src)</c> images.
/// </summary>
public sealed class MarkdownRenderer
{
    /// <summary>Converts an FO document string to Markdown.</summary>
    public string Convert(string foXml)
    {
        ArgumentNullException.ThrowIfNull(foXml);
        return Render(FoTreeBuilder.ParseString(foXml));
    }

    /// <summary>Converts an FO document stream to Markdown, written (UTF-8) to <paramref name="output"/>.</summary>
    public void Convert(Stream foInput, Stream output)
    {
        ArgumentNullException.ThrowIfNull(foInput);
        ArgumentNullException.ThrowIfNull(output);
        Render(FoTreeBuilder.Parse(foInput), output);
    }

    /// <summary>Renders an already-parsed FO tree to Markdown, written (UTF-8) to <paramref name="output"/>.</summary>
    public void Render(FoRoot root, Stream output)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(output);
        using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
        Render(root, writer);
    }

    /// <summary>Renders an already-parsed FO tree to Markdown.</summary>
    public string Render(FoRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);
        using var writer = new StringWriter();
        Render(root, writer);
        return writer.ToString();
    }

    private void Render(FoRoot root, TextWriter writer)
    {
        // Stream straight to the writer; the trimmer drops the document's trailing blank lines and
        // emits the single terminating newline the back-end guarantees.
        var trimmer = new TrailingWhitespaceTrimmer(writer);
        WriteBlocks(trimmer, DocExtractor.Extract(root));
        trimmer.FinishWithNewline();
    }

    private void WriteBlocks(TextWriter writer, IReadOnlyList<DocBlock> blocks)
    {
        foreach (DocBlock block in blocks)
        {
            switch (block)
            {
                case DocParagraph p:
                    bool heading = p.HeadingLevel is > 0 and <= 6;
                    // A heading is already emphasised, so don't also wrap its text in bold markers.
                    string text = Inline(p.Inlines, suppressBold: heading);
                    if (text.Length == 0)
                    {
                        break;
                    }

                    if (heading)
                    {
                        writer.Write(new string('#', p.HeadingLevel));
                        writer.Write(' ');
                    }

                    writer.Write(text);
                    writer.Write("\n\n");
                    break;

                case DocList list:
                    foreach (DocListItem item in list.Items)
                    {
                        writer.Write("- ");
                        writer.Write(FlattenInline(item.Body));
                        writer.Write('\n');
                    }

                    writer.Write('\n');
                    break;

                case DocTable table:
                    WriteTable(writer, table);
                    break;

                case DocImage image:
                    writer.Write("![");
                    writer.Write(Escape(image.Alt));
                    writer.Write("](");
                    writer.Write(image.Source);
                    writer.Write(")\n\n");
                    break;
            }
        }
    }

    private void WriteTable(TextWriter writer, DocTable table)
    {
        if (table.Rows.Count == 0)
        {
            return;
        }

        int columns = table.Rows.Max(r => r.Cells.Sum(c => c.ColumnSpan));

        // GitHub tables require a header row; use the first row (synthesizing blank headers if the
        // table has no explicit header row).
        DocTableRow first = table.Rows[0];
        WriteRow(writer, first, columns);
        writer.Write('|');
        writer.Write(string.Concat(Enumerable.Repeat(" --- |", columns)));
        writer.Write('\n');
        foreach (DocTableRow row in table.Rows.Skip(1))
        {
            WriteRow(writer, row, columns);
        }

        writer.Write('\n');
    }

    private void WriteRow(TextWriter writer, DocTableRow row, int columns)
    {
        writer.Write('|');
        int emitted = 0;
        foreach (DocTableCell cell in row.Cells)
        {
            string text = FlattenInline(cell.Body).Replace("|", "\\|");
            writer.Write(' ');
            writer.Write(text);
            writer.Write(" |");
            emitted += cell.ColumnSpan;
            for (int i = 1; i < cell.ColumnSpan; i++)
            {
                writer.Write("  |"); // spanned columns as empty cells (Markdown has no colspan)
            }
        }

        for (; emitted < columns; emitted++)
        {
            writer.Write("  |");
        }

        writer.Write('\n');
    }

    private string Inline(IReadOnlyList<DocInline> inlines, bool suppressBold = false)
    {
        var sb = new StringBuilder();
        foreach (DocInline run in inlines)
        {
            string text = Escape(run.Text);
            // Apply emphasis to the trimmed core, preserving surrounding spaces so markers hug the word.
            string lead = text[..(text.Length - text.TrimStart().Length)];
            string trail = text[text.TrimEnd().Length..];
            string core = text.Trim();
            if (core.Length > 0)
            {
                if (run.Bold && !suppressBold)
                {
                    core = "**" + core + "**";
                }

                if (run.Italic)
                {
                    core = "_" + core + "_";
                }

                if (run.Uri is { Length: > 0 } uri)
                {
                    core = "[" + core + "](" + uri + ")";
                }
            }

            sb.Append(lead).Append(core).Append(trail);
        }

        return sb.ToString().Trim();
    }

    private string FlattenInline(IReadOnlyList<DocBlock> blocks)
    {
        var parts = new List<string>();
        foreach (DocBlock block in blocks)
        {
            switch (block)
            {
                case DocParagraph p:
                    string t = Inline(p.Inlines);
                    if (t.Length > 0)
                    {
                        parts.Add(t);
                    }

                    break;
                case DocList list:
                    parts.AddRange(list.Items.Select(i => FlattenInline(i.Body)));
                    break;
                case DocImage image:
                    parts.Add("![" + Escape(image.Alt) + "](" + image.Source + ")");
                    break;
            }
        }

        return string.Join(' ', parts);
    }

    /// <summary>Escapes the Markdown metacharacters that would otherwise be interpreted in text.</summary>
    private static string Escape(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (c is '\\' or '`' or '*' or '_' or '[' or ']' or '<' or '>')
            {
                sb.Append('\\');
            }

            sb.Append(c);
        }

        return sb.ToString();
    }
}
