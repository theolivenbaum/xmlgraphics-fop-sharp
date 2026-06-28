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

using System.Linq;

using Fop.Colors;
using Fop.Fo;

using Xunit;

namespace Fop.Layout.Tests;

/// <summary>
/// Layout tests for the transparent <c>fo:wrapper</c>: it generates no area of its own but carries
/// inherited properties onto its children, which lay out inline or in the block flow.
/// </summary>
public sealed class WrapperLayoutTests
{
    private static readonly FakeFontMeasurer Measurer = new();

    private static AreaTree LayOut(string body) =>
        new LayoutEngine(Measurer).LayOut(FoTreeBuilder.ParseString($"""
            <fo:root xmlns:fo="http://www.w3.org/1999/XSL/Format" font-size="10pt">
              <fo:layout-master-set>
                <fo:simple-page-master master-name="p" page-width="300pt" page-height="300pt">
                  <fo:region-body/>
                </fo:simple-page-master>
              </fo:layout-master-set>
              <fo:page-sequence master-reference="p">
                <fo:flow flow-name="xsl-region-body">{body}</fo:flow>
              </fo:page-sequence>
            </fo:root>
            """));

    [Fact]
    public void InlineWrapperAppliesItsColourToChildText()
    {
        // The wrapper sets colour; its inline text inherits it (the wrapper generates no area itself).
        AreaTree tree = LayOut("""
            <fo:block>plain <fo:wrapper color="#ff0000">red</fo:wrapper></fo:block>
            """);
        PageArea page = Assert.Single(tree.Pages);

        TextRun red = page.TextRuns.Single(r => r.Text == "red");
        Assert.Equal(0xFF, red.Color.Red);
        Assert.Equal(0x00, red.Color.Green);
        TextRun plain = page.TextRuns.Single(r => r.Text == "plain");
        Assert.Equal(0x00, plain.Color.Red);
    }

    [Fact]
    public void BlockLevelWrapperStacksItsBlockChildren()
    {
        // A wrapper containing blocks is transparent: its block children stack in the flow rather than
        // being dropped. Both blocks must appear, in order.
        AreaTree tree = LayOut("""
            <fo:wrapper color="#0000ff">
              <fo:block>one</fo:block>
              <fo:block>two</fo:block>
            </fo:wrapper>
            """);
        PageArea page = Assert.Single(tree.Pages);

        var order = page.TextRuns.OrderBy(r => r.BaselineYMpt).Select(r => r.Text).ToArray();
        Assert.Equal(new[] { "one", "two" }, order);
    }

    [Fact]
    public void BlockLevelWrapperPropagatesInheritedPropertiesToChildren()
    {
        // The wrapper's colour is inherited by its block children (which set no colour of their own).
        AreaTree tree = LayOut("""
            <fo:wrapper color="#0000ff"><fo:block>blue</fo:block></fo:wrapper>
            """);
        PageArea page = Assert.Single(tree.Pages);

        TextRun blue = page.TextRuns.Single(r => r.Text == "blue");
        Assert.Equal(0x00, blue.Color.Red);
        Assert.Equal(0xFF, blue.Color.Blue);
    }

    [Fact]
    public void NestedBlockWrappersAreFullyTransparent()
    {
        // Wrappers nested inside wrappers still contribute their innermost block children to the flow.
        AreaTree tree = LayOut("""
            <fo:wrapper>
              <fo:wrapper><fo:block>deep</fo:block></fo:wrapper>
            </fo:wrapper>
            """);
        PageArea page = Assert.Single(tree.Pages);
        Assert.Contains(page.TextRuns, r => r.Text == "deep");
    }
}
