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
/// Renders an XSL-FO document's logical content to plain UTF-8 text: paragraphs separated by blank
/// lines, list items bulleted, table rows tab-separated, images shown as a bracketed placeholder.
/// Emphasis and links are dropped (plain text carries no styling).
/// </summary>
public sealed class PlainTextRenderer
{
    /// <summary>Converts an FO document string to plain text.</summary>
    public string Convert(string foXml)
    {
        ArgumentNullException.ThrowIfNull(foXml);
        return Render(FoTreeBuilder.ParseString(foXml));
    }

    /// <summary>Converts an FO document stream to plain text, written (UTF-8) to <paramref name="output"/>.</summary>
    public void Convert(Stream foInput, Stream output)
    {
        ArgumentNullException.ThrowIfNull(foInput);
        ArgumentNullException.ThrowIfNull(output);
        Render(FoTreeBuilder.Parse(foInput), output);
    }

    /// <summary>Renders an already-parsed FO tree to plain text, written (UTF-8) to <paramref name="output"/>.</summary>
    public void Render(FoRoot root, Stream output)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(output);
        using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
        Render(root, writer);
    }

    /// <summary>Renders an already-parsed FO tree to plain text.</summary>
    public string Render(FoRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);
        using var writer = new StringWriter();
        Render(root, writer);
        return writer.ToString();
    }

    private static void Render(FoRoot root, TextWriter writer)
    {
        // Stream straight to the writer; the trimmer drops the document's trailing blank lines and
        // emits the single terminating newline the back-end guarantees.
        var trimmer = new TrailingWhitespaceTrimmer(writer);
        WriteBlocks(trimmer, DocExtractor.Extract(root), indent: string.Empty);
        trimmer.FinishWithNewline();
    }

    private static void WriteBlocks(TextWriter writer, IReadOnlyList<DocBlock> blocks, string indent)
    {
        foreach (DocBlock block in blocks)
        {
            switch (block)
            {
                case DocParagraph p:
                    string text = InlineText(p.Inlines);
                    if (text.Length > 0)
                    {
                        writer.Write(indent);
                        writer.Write(text);
                        writer.Write("\n\n");
                    }

                    break;

                case DocList list:
                    foreach (DocListItem item in list.Items)
                    {
                        writer.Write(indent);
                        writer.Write("  - ");
                        writer.Write(FlattenBlocks(item.Body));
                        writer.Write('\n');
                    }

                    writer.Write('\n');
                    break;

                case DocTable table:
                    foreach (DocTableRow row in table.Rows)
                    {
                        writer.Write(indent);
                        writer.Write(string.Join('\t', row.Cells.Select(c => FlattenBlocks(c.Body))));
                        writer.Write('\n');
                    }

                    writer.Write('\n');
                    break;

                case DocImage image:
                    writer.Write(indent);
                    writer.Write('[');
                    writer.Write(image.Source.Length > 0 ? "image: " + image.Source : image.Alt);
                    writer.Write("]\n\n");
                    break;
            }
        }
    }

    private static string InlineText(IReadOnlyList<DocInline> inlines) =>
        string.Concat(inlines.Select(i => i.Text)).Trim();

    /// <summary>Flattens block content to a single line (for a list label / table cell).</summary>
    private static string FlattenBlocks(IReadOnlyList<DocBlock> blocks)
    {
        var parts = new List<string>();
        foreach (DocBlock block in blocks)
        {
            switch (block)
            {
                case DocParagraph p:
                    string t = InlineText(p.Inlines);
                    if (t.Length > 0)
                    {
                        parts.Add(t);
                    }

                    break;
                case DocList list:
                    parts.AddRange(list.Items.Select(i => FlattenBlocks(i.Body)));
                    break;
                case DocImage image:
                    parts.Add(image.Source.Length > 0 ? "[image: " + image.Source + "]" : "[" + image.Alt + "]");
                    break;
            }
        }

        return string.Join(' ', parts);
    }
}
