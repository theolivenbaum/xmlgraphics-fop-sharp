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

using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Fop.Fo;
using Fop.Layout;
using Fop.Render.Pdf;
using Fop.Render.Pdf.Native;
using PdfSharp.Pdf.IO;
using Xunit;

namespace Fop.Rendering.Tests;

/// <summary>
/// Tests that a <see cref="BackgroundImageArea"/> is painted as a clipped, tiled image XObject by both
/// the native and PdfSharp PDF renderers, mirroring FOP's <c>drawBackground</c>.
/// </summary>
public class BackgroundImageRenderingTests
{
    // A 2x2 RGB PNG (no resolution metadata -> 72 dpi -> a 2pt = 2000mpt tile).
    private const string Rgb2x2 =
        "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAFElEQVR4nGP4z8DAAMIM////ZwAAHu8E/KPItPcAAAAASUVORK5CYII=";

    private static byte[] RenderNative(AreaTree tree)
    {
        using var output = new MemoryStream();
        new NativePdfRenderer(new PdfSharpFontMeasurer()).Render(tree, output);
        return output.ToArray();
    }

    private static byte[] RenderPdfSharp(AreaTree tree)
    {
        using var output = new MemoryStream();
        new PdfRenderer(new PdfSharpFontMeasurer()).Render(tree, output);
        return output.ToArray();
    }

    private static AreaTree TreeWith(BackgroundImageArea background)
    {
        var tree = new AreaTree();
        var page = new PageArea(200_000, 200_000);
        page.Add(background);
        tree.AddPage(page);
        return tree;
    }

    private static string Latin1(byte[] pdf) => Encoding.Latin1.GetString(pdf);

    private static string InflatedStreams(byte[] pdf)
    {
        string text = Latin1(pdf);
        var sb = new StringBuilder();
        foreach (Match m in Regex.Matches(text, "stream\r?\n"))
        {
            int start = m.Index + m.Length;
            int end = text.IndexOf("endstream", start, StringComparison.Ordinal);
            if (end < 0)
            {
                continue;
            }

            byte[] raw = pdf[start..end];
            try
            {
                using var zin = new ZLibStream(new MemoryStream(raw), CompressionMode.Decompress);
                using var outMs = new MemoryStream();
                zin.CopyTo(outMs);
                sb.Append(Encoding.Latin1.GetString(outMs.ToArray()));
            }
            catch
            {
                // Non-zlib stream; skip.
            }
        }

        return sb.ToString();
    }

    private static int Count(string haystack, string needle)
    {
        int count = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0)
        {
            count++;
            i += needle.Length;
        }

        return count;
    }

    [Fact]
    public void Native_NoRepeat_ClipsAndDrawsSingleTile()
    {
        // 20000x10000 padding rect, no-repeat -> exactly one tile, clipped to the rect.
        AreaTree tree = TreeWith(new BackgroundImageArea(
            10_000, 10_000, 20_000, 10_000, SourcePath: null,
            Convert.FromBase64String(Rgb2x2), BackgroundRepeat.NoRepeat,
            BackgroundPosition.Zero, BackgroundPosition.Zero));

        byte[] pdf = RenderNative(tree);
        string streams = InflatedStreams(pdf);

        Assert.Contains("/Subtype /Image", Latin1(pdf));
        Assert.Contains(" re W n", streams);       // clip to the padding rectangle
        Assert.Equal(1, Count(streams, " Do"));     // a single image draw
    }

    private static int TileDrawCount(BackgroundRepeat repeat)
    {
        AreaTree tree = TreeWith(new BackgroundImageArea(
            0, 0, 20_000, 10_000, SourcePath: null,
            Convert.FromBase64String(Rgb2x2), repeat,
            BackgroundPosition.Zero, BackgroundPosition.Zero));
        return Count(InflatedStreams(RenderNative(tree)), " Do");
    }

    [Fact]
    public void Native_Repeat_TilesBothAxes_DecomposingIntoRepeatXTimesRepeatY()
    {
        // The 2D tiling decomposes into (horizontal-only) x (vertical-only). This holds regardless of the
        // image's resolution (so the test does not hardcode the decoder's default DPI).
        int repeat = TileDrawCount(BackgroundRepeat.Repeat);
        int repeatX = TileDrawCount(BackgroundRepeat.RepeatX);
        int repeatY = TileDrawCount(BackgroundRepeat.RepeatY);

        Assert.True(repeatX > 1, "repeat-x should tile horizontally");
        Assert.True(repeatY > 1, "repeat-y should tile vertically");
        Assert.Equal(repeatX * repeatY, repeat);
        Assert.Contains(" re W n", InflatedStreams(RenderNative(TreeWith(new BackgroundImageArea(
            0, 0, 20_000, 10_000, SourcePath: null, Convert.FromBase64String(Rgb2x2),
            BackgroundRepeat.Repeat, BackgroundPosition.Zero, BackgroundPosition.Zero)))));
    }

    [Fact]
    public void Native_NoRepeat_DrawsExactlyOneTileRegardlessOfResolution()
    {
        Assert.Equal(1, TileDrawCount(BackgroundRepeat.NoRepeat));
    }

    [Fact]
    public void Native_OutputReopensInPdfSharp()
    {
        AreaTree tree = TreeWith(new BackgroundImageArea(
            0, 0, 20_000, 10_000, SourcePath: null,
            Convert.FromBase64String(Rgb2x2), BackgroundRepeat.Repeat,
            BackgroundPosition.Zero, BackgroundPosition.Zero));

        byte[] pdf = RenderNative(tree);
        using var input = new MemoryStream(pdf);
        using var doc = PdfReader.Open(input, PdfDocumentOpenMode.Import);
        Assert.Equal(1, doc.PageCount);
    }

    [Fact]
    public void Native_UndecodableImageIsSkipped()
    {
        AreaTree tree = TreeWith(new BackgroundImageArea(
            0, 0, 20_000, 10_000, SourcePath: null, new byte[] { 1, 2, 3, 4 },
            BackgroundRepeat.Repeat, BackgroundPosition.Zero, BackgroundPosition.Zero));

        byte[] pdf = RenderNative(tree);
        Assert.DoesNotContain("/Subtype /Image", Latin1(pdf));
    }

    [Fact]
    public void PdfSharp_RendersBackgroundImageAndReopens()
    {
        AreaTree tree = TreeWith(new BackgroundImageArea(
            0, 0, 20_000, 10_000, SourcePath: null,
            Convert.FromBase64String(Rgb2x2), BackgroundRepeat.NoRepeat,
            BackgroundPosition.Zero, BackgroundPosition.Zero));

        byte[] pdf = RenderPdfSharp(tree);
        using var input = new MemoryStream(pdf);
        using var doc = PdfReader.Open(input, PdfDocumentOpenMode.Import);
        Assert.Equal(1, doc.PageCount);
        Assert.Contains("/Image", Latin1(pdf));
    }

    [Fact]
    public void EndToEnd_BackgroundImageFlowsThroughFoPipeline()
    {
        // Write the PNG to a temp file referenced from the FO; the engine emits a background-image area.
        string dir = Path.Combine(Path.GetTempPath(), "fop-bgimg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string imagePath = Path.Combine(dir, "bg.png");
        try
        {
            File.WriteAllBytes(imagePath, Convert.FromBase64String(Rgb2x2));
            string uri = imagePath.Replace("\\", "/");
            string fo = $"""
                <fo:root xmlns:fo="http://www.w3.org/1999/XSL/Format">
                  <fo:layout-master-set>
                    <fo:simple-page-master master-name="p" page-width="200pt" page-height="200pt">
                      <fo:region-body/>
                    </fo:simple-page-master>
                  </fo:layout-master-set>
                  <fo:page-sequence master-reference="p">
                    <fo:flow flow-name="xsl-region-body">
                      <fo:block background-image="url('{uri}')" background-repeat="repeat"
                          padding="10pt">content</fo:block>
                    </fo:flow>
                  </fo:page-sequence>
                </fo:root>
                """;

            AreaTree tree = new LayoutEngine(new PdfSharpFontMeasurer(), new PdfSharpImageResolver())
                .LayOut(FoTreeBuilder.ParseString(fo));
            BackgroundImageArea bg = Assert.Single(Assert.Single(tree.Pages).BackgroundImages);
            Assert.Equal(imagePath, bg.SourcePath);

            byte[] pdf = RenderNative(tree);
            Assert.Contains("/Subtype /Image", Latin1(pdf));
            Assert.Contains(" Do", InflatedStreams(pdf));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
