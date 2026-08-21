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
using System.Text;
using Fop.Layout;
using Fop.Render.Pdf;
using Xunit;

namespace Fop.Rendering.Tests;

/// <summary>
/// End-to-end cover for <see cref="FopProcessor.ResourceResolver"/>: an <c>fo:external-graphic</c>
/// whose <c>src</c> names nothing on the local disk is resolved by the application, and the bytes it
/// returns are embedded in the PDF exactly as a file's would have been.
/// <para>
/// This is the case that used to force a caller to write every image out to a temporary file first,
/// because the renderers could only open a path.
/// </para>
/// </summary>
public class ResourceResolverRenderingTests
{
    /// <summary>A 2x2 RGB PNG.</summary>
    private const string Rgb2x2 =
        "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAFElEQVR4nGP4z8DAAMIM////ZwAAHu8E/KPItPcAAAAASUVORK5CYII=";

    private static string Document(string src) =>
        $"""
        <fo:root xmlns:fo="http://www.w3.org/1999/XSL/Format">
          <fo:layout-master-set>
            <fo:simple-page-master master-name="p" page-width="200pt" page-height="200pt"
                                   margin="10pt">
              <fo:region-body/>
            </fo:simple-page-master>
          </fo:layout-master-set>
          <fo:page-sequence master-reference="p">
            <fo:flow flow-name="xsl-region-body">
              <fo:block><fo:external-graphic src="{src}" content-width="60pt"/></fo:block>
            </fo:flow>
          </fo:page-sequence>
        </fo:root>
        """;

    private static FopProcessor WithStore(byte[] png) => new()
    {
        ResourceResolver = ResourceResolvers.FromDelegate(uri =>
            uri == "icn:GA-01" ? new MemoryStream(png, writable: false) : null),
    };

    /// <summary>
    /// The number of image XObjects in the PDF. The native renderer embeds a raster image as one, so
    /// this is what separates "the image is in the file" from "the area was reserved and left empty".
    /// </summary>
    private static int ImageObjects(byte[] pdf)
    {
        string text = Encoding.Latin1.GetString(pdf);
        int count = 0;
        for (int i = text.IndexOf("/Subtype /Image", StringComparison.Ordinal); i >= 0;
             i = text.IndexOf("/Subtype /Image", i + 1, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    [Fact]
    public void AResolvedImageIsEmbeddedJustAsAFileWouldBe()
    {
        byte[] png = Convert.FromBase64String(Rgb2x2);

        byte[] resolved = WithStore(png).ConvertNative(Document("icn:GA-01"));

        Assert.Equal("%PDF", Encoding.ASCII.GetString(resolved[..4]));
        Assert.Equal(1, ImageObjects(resolved));
    }

    [Fact]
    public void WithoutAResolverTheSameDocumentHasNoImageInIt()
    {
        // The control for the test above, and the behaviour every FO document naming a URI the
        // engine cannot open still gets: the area is reserved and nothing is drawn in it.
        byte[] unresolved = new FopProcessor().ConvertNative(Document("icn:GA-01"));

        Assert.Equal(0, ImageObjects(unresolved));
    }

    [Fact]
    public void ResolvedBytesAndTheSameFileOnDiskProduceTheSamePdf()
    {
        byte[] png = Convert.FromBase64String(Rgb2x2);
        string directory = Directory.CreateTempSubdirectory("fop-resolver-render-").FullName;
        try
        {
            string path = Path.Combine(directory, "plate.png");
            File.WriteAllBytes(path, png);

            byte[] fromDisk = new FopProcessor().ConvertNative(Document(path.Replace("\\", "/")));
            byte[] fromStore = WithStore(png).ConvertNative(Document("icn:GA-01"));

            // Byte-for-byte, because the resolver feeds the identical bytes into the identical
            // pipeline. If these ever diverge, the hook is not equivalent to a file and the
            // difference is a bug rather than a detail.
            Assert.Equal(fromDisk, fromStore);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ADataUriIsEmbeddedWithNoResolverAtAll()
    {
        byte[] pdf = new FopProcessor().ConvertNative(Document("data:image/png;base64," + Rgb2x2));

        Assert.Equal(1, ImageObjects(pdf));
    }

    [Fact]
    public void ThePdfSharpPathEmbedsAResolvedImageToo()
    {
        byte[] png = Convert.FromBase64String(Rgb2x2);

        byte[] pdf = WithStore(png).Convert(Document("icn:GA-01"));

        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf[..4]));

        // PdfSharp writes its own object structure, so assert on size against the same document
        // with nothing resolved rather than on its spelling of an image dictionary.
        byte[] empty = new FopProcessor().Convert(Document("icn:GA-01"));
        Assert.True(pdf.Length > empty.Length,
            $"expected the embedded image to cost bytes: {pdf.Length} vs {empty.Length}");
    }
}
