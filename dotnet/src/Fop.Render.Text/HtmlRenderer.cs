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
/// Renders an XSL-FO document's logical content to a semantic HTML5 document: headings, paragraphs
/// with <c>&lt;strong&gt;</c>/<c>&lt;em&gt;</c>/<c>&lt;a&gt;</c>, <c>&lt;ul&gt;</c> lists,
/// <c>&lt;table&gt;</c>s (header rows as <c>&lt;th&gt;</c>) and <c>&lt;img&gt;</c>s. All text is
/// HTML-escaped.
/// </summary>
public sealed class HtmlRenderer
{
    /// <summary>Converts an FO document string to an HTML document.</summary>
    public string Convert(string foXml)
    {
        ArgumentNullException.ThrowIfNull(foXml);
        return Render(FoTreeBuilder.ParseString(foXml));
    }

    /// <summary>Converts an FO document stream to HTML, written (UTF-8) to <paramref name="output"/>.</summary>
    public void Convert(Stream foInput, Stream output)
    {
        ArgumentNullException.ThrowIfNull(foInput);
        ArgumentNullException.ThrowIfNull(output);
        Render(FoTreeBuilder.Parse(foInput), output);
    }

    /// <summary>Renders an already-parsed FO tree to HTML, written (UTF-8) to <paramref name="output"/>.</summary>
    public void Render(FoRoot root, Stream output)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(output);
        using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
        Render(root, writer);
    }

    /// <summary>Renders an already-parsed FO tree to an HTML document.</summary>
    public string Render(FoRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);
        using var writer = new StringWriter();
        Render(root, writer);
        return writer.ToString();
    }

    private void Render(FoRoot root, TextWriter writer)
    {
        writer.Write("<!DOCTYPE html>\n<html>\n<head>\n<meta charset=\"utf-8\">\n</head>\n<body>\n");
        WriteBlocks(writer, DocExtractor.Extract(root), indent: "  ");
        writer.Write("</body>\n</html>\n");
    }

    private void WriteBlocks(TextWriter writer, IReadOnlyList<DocBlock> blocks, string indent)
    {
        foreach (DocBlock block in blocks)
        {
            switch (block)
            {
                case DocParagraph p:
                    bool heading = p.HeadingLevel is > 0 and <= 6;
                    // A heading element is already emphasised; don't also wrap its text in <strong>.
                    string content = Inline(p.Inlines, suppressBold: heading);
                    if (content.Length == 0)
                    {
                        break;
                    }

                    string tag = heading ? "h" + p.HeadingLevel : "p";
                    writer.Write(indent);
                    writer.Write('<');
                    writer.Write(tag);
                    writer.Write('>');
                    writer.Write(content);
                    writer.Write("</");
                    writer.Write(tag);
                    writer.Write(">\n");
                    break;

                case DocList list:
                    writer.Write(indent);
                    writer.Write("<ul>\n");
                    foreach (DocListItem item in list.Items)
                    {
                        writer.Write(indent);
                        writer.Write("  <li>");
                        WriteInlineOrBlocks(writer, item.Body, indent + "  ");
                        writer.Write("</li>\n");
                    }

                    writer.Write(indent);
                    writer.Write("</ul>\n");
                    break;

                case DocTable table:
                    WriteTable(writer, table, indent);
                    break;

                case DocImage image:
                    writer.Write(indent);
                    writer.Write("<img src=\"");
                    writer.Write(AttrEscape(image.Source));
                    writer.Write("\" alt=\"");
                    writer.Write(AttrEscape(image.Alt));
                    writer.Write("\">\n");
                    break;
            }
        }
    }

    private void WriteTable(TextWriter writer, DocTable table, string indent)
    {
        writer.Write(indent);
        writer.Write("<table>\n");
        foreach (DocTableRow row in table.Rows)
        {
            writer.Write(indent);
            writer.Write("  <tr>");
            string cellTag = row.IsHeader ? "th" : "td";
            foreach (DocTableCell cell in row.Cells)
            {
                writer.Write('<');
                writer.Write(cellTag);
                if (cell.ColumnSpan > 1)
                {
                    writer.Write(" colspan=\"");
                    writer.Write(cell.ColumnSpan);
                    writer.Write('"');
                }

                writer.Write('>');
                WriteInlineOrBlocks(writer, cell.Body, indent);
                writer.Write("</");
                writer.Write(cellTag);
                writer.Write('>');
            }

            writer.Write("</tr>\n");
        }

        writer.Write(indent);
        writer.Write("</table>\n");
    }

    /// <summary>
    /// Writes a cell/list-item body: a single paragraph emits just its inline content, while richer
    /// content (nested lists, multiple paragraphs, images) is emitted as full block markup.
    /// </summary>
    private void WriteInlineOrBlocks(TextWriter writer, IReadOnlyList<DocBlock> body, string indent)
    {
        if (body is [DocParagraph only])
        {
            writer.Write(Inline(only.Inlines));
            return;
        }

        writer.Write('\n');
        WriteBlocks(writer, body, indent + "  ");
        writer.Write(indent);
    }

    private string Inline(IReadOnlyList<DocInline> inlines, bool suppressBold = false)
    {
        var sb = new StringBuilder();
        foreach (DocInline run in inlines)
        {
            string text = Escape(run.Text);
            if (run.Bold && !suppressBold)
            {
                text = "<strong>" + text + "</strong>";
            }

            if (run.Italic)
            {
                text = "<em>" + text + "</em>";
            }

            if (run.Uri is { Length: > 0 } uri)
            {
                text = "<a href=\"" + AttrEscape(uri) + "\">" + text + "</a>";
            }

            sb.Append(text);
        }

        return sb.ToString().Trim();
    }

    private static string Escape(string text) => text
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string AttrEscape(string text) => Escape(text).Replace("\"", "&quot;");
}
