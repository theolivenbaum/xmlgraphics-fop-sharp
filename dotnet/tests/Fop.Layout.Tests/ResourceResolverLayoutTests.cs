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
using Xunit;

namespace Fop.Layout.Tests;

/// <summary>
/// Tests for <see cref="LayoutEngine.ResourceResolver"/>: an image URI that names neither a file on
/// disk nor a "data:" payload is offered to the application, which answers with bytes. Covers
/// <c>fo:external-graphic</c> and <c>background-image</c>, the built-in "data:" decoding, the
/// fall-through when nothing resolves, and the one-call-per-URI caching that a two-pass layout makes
/// necessary.
/// </summary>
public sealed class ResourceResolverLayoutTests
{
    private static readonly FakeFontMeasurer Measurer = new();

    /// <summary>Payload bytes distinctive enough to assert on.</summary>
    private static readonly byte[] Payload = [0x89, (byte)'P', (byte)'N', (byte)'G', 1, 2, 3, 4];

    /// <summary>Records what it was asked for, so the caching claim can be tested.</summary>
    private sealed class RecordingResolver(Func<string, byte[]?> bytes) : IResourceResolver
    {
        public List<string> Requests { get; } = [];

        public Stream? GetResource(string uri)
        {
            Requests.Add(uri);
            byte[]? found = bytes(uri);
            return found is null ? null : new MemoryStream(found, writable: false);
        }
    }

    private static string Document(string body) =>
        $"""
        <fo:root xmlns:fo="http://www.w3.org/1999/XSL/Format">
          <fo:layout-master-set>
            <fo:simple-page-master master-name="p" page-width="1000pt" page-height="1000pt">
              <fo:region-body/>
            </fo:simple-page-master>
          </fo:layout-master-set>
          <fo:page-sequence master-reference="p">
            <fo:flow flow-name="xsl-region-body">
        {body}
            </fo:flow>
          </fo:page-sequence>
        </fo:root>
        """;

    private static AreaTree LayOut(string body, IResourceResolver? resolver)
    {
        var engine = new LayoutEngine(Measurer) { ResourceResolver = resolver };
        return engine.LayOut(FoTreeBuilder.ParseString(Document(body)));
    }

    private static string Graphic(string src) =>
        $"""      <fo:block><fo:external-graphic src="{src}" content-width="40pt"/></fo:block>""";

    // ------------------------------------------------------------------------
    // fo:external-graphic
    // ------------------------------------------------------------------------

    [Fact]
    public void ResolverSuppliesBytesForAUriThatIsNotAPath()
    {
        var resolver = new RecordingResolver(uri => uri == "icn:GA-01" ? Payload : null);

        ImageRun image = Assert.Single(LayOut(Graphic("icn:GA-01"), resolver).Pages[0].Images);

        // The area tree carries the bytes, and no path: there is nothing on disk for a renderer to
        // open, which is the whole point of the hook.
        Assert.Equal(Payload, image.SourceBytes);
        Assert.Null(image.SourcePath);
        Assert.Equal(["icn:GA-01"], resolver.Requests);
    }

    [Fact]
    public void AUriTheResolverDoesNotHaveIsStillPassedOnAsAPath()
    {
        var resolver = new RecordingResolver(_ => null);

        ImageRun image = Assert.Single(LayOut(Graphic("images/plate.png"), resolver).Pages[0].Images);

        // Unchanged behaviour: the renderer opens it, and draws an empty area if it cannot.
        Assert.Equal("images/plate.png", image.SourcePath);
        Assert.Null(image.SourceBytes);
    }

    [Fact]
    public void NoResolverAtAllLeavesEveryUriAsAPath()
    {
        ImageRun image = Assert.Single(LayOut(Graphic("images/plate.png"), resolver: null).Pages[0].Images);

        Assert.Equal("images/plate.png", image.SourcePath);
        Assert.Null(image.SourceBytes);
    }

    [Fact]
    public void DataUrisAreDecodedWithoutAResolver()
    {
        string uri = "data:image/png;base64," + System.Convert.ToBase64String(Payload);

        ImageRun image = Assert.Single(LayOut(Graphic(uri), resolver: null).Pages[0].Images);

        // Self-contained, so there is nothing for a renderer to open and nothing for an
        // application to resolve.
        Assert.Equal(Payload, image.SourceBytes);
        Assert.Null(image.SourcePath);
    }

    [Fact]
    public void DataUrisNeverReachTheResolver()
    {
        var resolver = new RecordingResolver(_ => null);
        string uri = "data:text/plain,hello";

        ImageRun image = Assert.Single(LayOut(Graphic(uri), resolver).Pages[0].Images);

        Assert.Equal(Encoding.ASCII.GetBytes("hello"), image.SourceBytes);
        Assert.Empty(resolver.Requests);
    }

    [Fact]
    public void AMalformedDataUriDegradesToAPathRatherThanThrowing()
    {
        // FOP treats an unreadable image as an empty area, never as a failed document.
        ImageRun image = Assert.Single(LayOut(Graphic("data:image/png;base64,%%%"), resolver: null)
            .Pages[0].Images);

        Assert.Null(image.SourceBytes);
        Assert.Equal("data:image/png;base64,%%%", image.SourcePath);
    }

    [Fact]
    public void AResolverThatThrowsDoesNotFailTheDocument()
    {
        var resolver = new RecordingResolver(_ => throw new InvalidOperationException("store is down"));

        ImageRun image = Assert.Single(LayOut(Graphic("icn:GA-01"), resolver).Pages[0].Images);

        Assert.Null(image.SourceBytes);
        Assert.Equal("icn:GA-01", image.SourcePath);
    }

    [Fact]
    public void TheResolvedBytesAreWhatTheIntrinsicSizeIsMeasuredFrom()
    {
        byte[]? seen = null;
        var images = new DelegatingImageResolver((path, bytes) =>
        {
            seen = bytes;
            return new ImageIntrinsics(200_000, 100_000);
        });

        var engine = new LayoutEngine(Measurer, images)
        {
            ResourceResolver = new RecordingResolver(_ => Payload),
        };
        AreaTree tree = engine.LayOut(FoTreeBuilder.ParseString(Document(
            """      <fo:block><fo:external-graphic src="icn:GA-01"/></fo:block>""")));

        // Layout and rendering must size the same bytes; before the hook the sizer was handed the
        // URI as a path and could only ever answer for a file.
        Assert.Equal(Payload, seen);
        Assert.Equal(200_000, Assert.Single(tree.Pages[0].Images).WidthMpt, 1);
    }

    private sealed class DelegatingImageResolver(Func<string?, byte[]?, ImageIntrinsics?> resolve)
        : IImageResolver
    {
        public ImageIntrinsics? Resolve(string? path, byte[]? bytes) => resolve(path, bytes);
    }

    // ------------------------------------------------------------------------
    // background-image
    // ------------------------------------------------------------------------

    [Fact]
    public void BackgroundImagesGoThroughTheResolverToo()
    {
        var resolver = new RecordingResolver(uri => uri == "icn:TILE" ? Payload : null);

        AreaTree tree = LayOut(
            """      <fo:block background-image="url('icn:TILE')" border="1pt solid black">Text</fo:block>""",
            resolver);

        BackgroundImageArea background = Assert.Single(tree.Pages[0].BackgroundImages);
        Assert.Equal(Payload, background.SourceBytes);
        Assert.Null(background.SourcePath);
    }

    // ------------------------------------------------------------------------
    // caching
    // ------------------------------------------------------------------------

    [Fact]
    public void AUriIsFetchedOnceHoweverOftenItAppears()
    {
        var resolver = new RecordingResolver(_ => Payload);

        AreaTree tree = LayOut(string.Join('\n', Enumerable.Repeat(Graphic("icn:GA-01"), 5)), resolver);

        // Five occurrences, and layout runs twice so page-number citations can resolve -- ten
        // fetches without the cache.
        Assert.Equal(5, tree.Pages[0].Images.Count);
        Assert.Equal(["icn:GA-01"], resolver.Requests);
    }

    [Fact]
    public void EachDocumentAsksAgain()
    {
        var resolver = new RecordingResolver(_ => Payload);
        var engine = new LayoutEngine(Measurer) { ResourceResolver = resolver };
        FoRoot root = FoTreeBuilder.ParseString(Document(Graphic("icn:GA-01")));

        engine.LayOut(root);
        engine.LayOut(root);

        // The cache is a within-layout optimisation, not a document-lifetime one: a second render
        // must see what the store now holds.
        Assert.Equal(["icn:GA-01", "icn:GA-01"], resolver.Requests);
    }

    // ------------------------------------------------------------------------
    // the shipped resolvers
    // ------------------------------------------------------------------------

    [Fact]
    public void ComposeTakesTheFirstResolverThatHasTheUri()
    {
        IResourceResolver composed = ResourceResolvers.Compose(
            null,
            ResourceResolvers.FromDelegate(uri => uri == "a" ? new MemoryStream([1]) : null),
            ResourceResolvers.FromDelegate(uri => uri is "a" or "b" ? new MemoryStream([2]) : null));

        Assert.Equal([1], Read(composed, "a"));
        Assert.Equal([2], Read(composed, "b"));
        Assert.Null(composed.GetResource("c"));
    }

    [Fact]
    public void ADirectoryResolverWillNotBeWalkedOutOfItsDirectory()
    {
        string directory = System.IO.Directory.CreateTempSubdirectory("fop-resolver-").FullName;
        try
        {
            File.WriteAllBytes(Path.Combine(directory, "plate.png"), Payload);
            IResourceResolver resolver = ResourceResolvers.Directory(directory);

            Assert.Equal(Payload, Read(resolver, "plate.png"));

            // The URI came out of a document, so only its last segment is ever joined to the
            // directory it was pointed at.
            Assert.Equal(Payload, Read(resolver, "some/other/place/plate.png"));
            Assert.Null(resolver.GetResource("../../etc/passwd"));
        }
        finally
        {
            System.IO.Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void TheEmptyResolverHasNothing() => Assert.Null(ResourceResolvers.None.GetResource("a"));

    private static byte[]? Read(IResourceResolver resolver, string uri)
    {
        using Stream? stream = resolver.GetResource(uri);
        if (stream is null)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
