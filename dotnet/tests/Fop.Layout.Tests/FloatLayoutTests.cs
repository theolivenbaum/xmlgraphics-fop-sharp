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

using Fop.Fo;

using Xunit;

namespace Fop.Layout.Tests;

/// <summary>
/// Layout tests for <c>fo:float</c>. Over the deterministic <see cref="FakeFontMeasurer"/> a 10pt line
/// is 12000mpt tall with its baseline 9000mpt below the line top, so positions are predictable.
/// </summary>
public sealed class FloatLayoutTests
{
    private static readonly FakeFontMeasurer Measurer = new();

    private static AreaTree LayOut(string body, double pageHeightPt = 200) =>
        new LayoutEngine(Measurer).LayOut(FoTreeBuilder.ParseString($"""
            <fo:root xmlns:fo="http://www.w3.org/1999/XSL/Format" font-size="10pt">
              <fo:layout-master-set>
                <fo:simple-page-master master-name="p" page-width="200pt" page-height="{pageHeightPt}pt">
                  <fo:region-body/>
                </fo:simple-page-master>
              </fo:layout-master-set>
              <fo:page-sequence master-reference="p">
                <fo:flow flow-name="xsl-region-body">{body}</fo:flow>
              </fo:page-sequence>
            </fo:root>
            """));

    [Fact]
    public void BeforeFloatAtFlowStartSitsAtTheRegionTop()
    {
        // A before-float first in the flow is placed at the region top; the following block flows below
        // it. The float line's baseline is 9000 (top region), the body block's baseline is 12000 + 9000.
        AreaTree tree = LayOut("""
            <fo:float float="before"><fo:block>F</fo:block></fo:float>
            <fo:block>B</fo:block>
            """);
        PageArea page = Assert.Single(tree.Pages);

        TextRun floatRun = page.TextRuns.Single(r => r.Text == "F");
        TextRun bodyRun = page.TextRuns.Single(r => r.Text == "B");
        Assert.Equal(9_000, floatRun.BaselineYMpt, 3);
        Assert.Equal(21_000, bodyRun.BaselineYMpt, 3);
    }

    [Fact]
    public void BeforeFloatAfterContentMovesToTheTopOfTheNextPage()
    {
        // The float is authored after a block, so the region is no longer empty: it defers to the top of
        // the next page rather than overlapping the placed content.
        AreaTree tree = LayOut("""
            <fo:block>B</fo:block>
            <fo:float float="before"><fo:block>F</fo:block></fo:float>
            """);
        Assert.Equal(2, tree.Pages.Count);

        Assert.Contains(tree.Pages[0].TextRuns, r => r.Text == "B");
        Assert.DoesNotContain(tree.Pages[0].TextRuns, r => r.Text == "F");

        TextRun floatRun = tree.Pages[1].TextRuns.Single(r => r.Text == "F");
        Assert.Equal(9_000, floatRun.BaselineYMpt, 3); // top of the next region
    }

    [Fact]
    public void BeforeFloatIsAnchoredAtTheTopOfAPageStartedByAnOverflow()
    {
        // Page holds three lines (36000 of 36pt). Three body blocks fill page 1; a before-float follows,
        // then a fourth block overflows onto page 2. The deferred float must sit at the top of page 2,
        // ahead of the overflow block.
        AreaTree tree = LayOut("""
            <fo:block>a</fo:block>
            <fo:block>b</fo:block>
            <fo:block>c</fo:block>
            <fo:float float="before"><fo:block>F</fo:block></fo:float>
            <fo:block>d</fo:block>
            """, pageHeightPt: 36);

        Assert.Equal(2, tree.Pages.Count);
        TextRun floatRun = tree.Pages[1].TextRuns.Single(r => r.Text == "F");
        TextRun overflow = tree.Pages[1].TextRuns.Single(r => r.Text == "d");
        Assert.Equal(9_000, floatRun.BaselineYMpt, 3);   // float at the region top
        Assert.True(overflow.BaselineYMpt > floatRun.BaselineYMpt); // body flows below the float
    }

    [Fact]
    public void StartFloatShiftsFollowingContentIntoTheRemainingColumn()
    {
        // An 80pt left float at the flow start reserves the left 80000mpt for its height (one line). The
        // following block wraps into the remaining column: shifted right to x=80000 and beside the float
        // (same baseline, since a side float does not advance the main cursor).
        AreaTree tree = LayOut("""
            <fo:float float="left" width="80pt"><fo:block>F</fo:block></fo:float>
            <fo:block>X</fo:block>
            <fo:block>Y</fo:block>
            """);
        PageArea page = Assert.Single(tree.Pages);

        TextRun f = page.TextRuns.Single(r => r.Text == "F");
        TextRun x = page.TextRuns.Single(r => r.Text == "X");
        TextRun y = page.TextRuns.Single(r => r.Text == "Y");

        Assert.Equal(0, f.XMpt, 3);          // float content at the left edge
        Assert.Equal(80_000, x.XMpt, 3);     // first block beside the float
        Assert.Equal(9_000, f.BaselineYMpt, 3);
        Assert.Equal(9_000, x.BaselineYMpt, 3); // same band as the float
        Assert.Equal(0, y.XMpt, 3);          // second block has cleared the float -> full width
        Assert.Equal(21_000, y.BaselineYMpt, 3);
    }

    [Fact]
    public void EndFloatNarrowsFollowingContentWithoutShiftingIt()
    {
        // An 80pt right float reserves the right 80000mpt. The float content sits at the right edge
        // (x=120000); the following block keeps the left edge (x=0) but is narrowed.
        AreaTree tree = LayOut("""
            <fo:float float="right" width="80pt"><fo:block>F</fo:block></fo:float>
            <fo:block>X</fo:block>
            """);
        PageArea page = Assert.Single(tree.Pages);

        TextRun f = page.TextRuns.Single(r => r.Text == "F");
        TextRun x = page.TextRuns.Single(r => r.Text == "X");
        Assert.Equal(120_000, f.XMpt, 3); // float content hugs the right edge
        Assert.Equal(0, x.XMpt, 3);       // following block keeps the left edge
        Assert.Equal(9_000, x.BaselineYMpt, 3);
    }

    [Fact]
    public void FloatNoneLaysOutInTheNormalFlow()
    {
        // float="none" is in-flow: the float's block sits between the two surrounding blocks.
        AreaTree tree = LayOut("""
            <fo:block>A</fo:block>
            <fo:float float="none"><fo:block>N</fo:block></fo:float>
            <fo:block>B</fo:block>
            """);
        PageArea page = Assert.Single(tree.Pages);

        var order = page.TextRuns.OrderBy(r => r.BaselineYMpt).Select(r => r.Text).ToArray();
        Assert.Equal(new[] { "A", "N", "B" }, order);
    }
}
