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

using Fop.Fo;
using Fop.Traits;

using Xunit;

namespace Fop.Fo.Tests;

public class BoxPropertyTests
{
    private static FoBlock FirstBlock(string blockMarkup)
    {
        string fo = $"""
            <fo:root xmlns:fo="http://www.w3.org/1999/XSL/Format" color="#000000">
              <fo:layout-master-set>
                <fo:simple-page-master master-name="p" page-width="200pt" page-height="200pt">
                  <fo:region-body/>
                </fo:simple-page-master>
              </fo:layout-master-set>
              <fo:page-sequence master-reference="p">
                <fo:flow flow-name="xsl-region-body">
                  {blockMarkup}
                </fo:flow>
              </fo:page-sequence>
            </fo:root>
            """;
        FoRoot root = FoTreeBuilder.ParseString(fo);
        return root.PageSequences.First().Flow!.ChildObjects.OfType<FoBlock>().First();
    }

    [Fact]
    public void BackgroundColor_Resolves()
    {
        FoBlock block = FirstBlock("<fo:block background-color=\"#ff0000\">x</fo:block>");
        BoxProperties box = block.Box;
        Assert.True(box.HasBackground);
        Assert.Equal(255, box.BackgroundColor!.Red);
        Assert.Equal(0, box.BackgroundColor.Green);
        Assert.Equal(0, box.BackgroundColor.Blue);
    }

    [Fact]
    public void BackgroundColor_TransparentIsNull()
    {
        FoBlock block = FirstBlock("<fo:block background-color=\"transparent\">x</fo:block>");
        Assert.False(block.Box.HasBackground);
    }

    [Fact]
    public void NoBoxProperties_IsEmpty()
    {
        FoBlock block = FirstBlock("<fo:block>x</fo:block>");
        Assert.True(block.Box.IsEmpty);
        Assert.False(block.Box.HasBorder);
    }

    [Fact]
    public void BorderShorthand_AppliesToAllFourEdges()
    {
        FoBlock block = FirstBlock("<fo:block border=\"2pt solid #00ff00\">x</fo:block>");
        BoxProperties box = block.Box;
        foreach (BorderEdge edge in new[] { box.BorderTop, box.BorderRight, box.BorderBottom, box.BorderLeft })
        {
            Assert.Equal(2_000, edge.Width.Millipoints, 3);
            Assert.Equal(BorderStyle.Solid, edge.Style);
            Assert.Equal(0, edge.Color.Red);
            Assert.Equal(255, edge.Color.Green);
            Assert.True(edge.IsVisible);
        }

        Assert.True(box.HasBorder);
    }

    [Fact]
    public void BorderColorDefaultsToCurrentColor()
    {
        // border with only width+style; colour should default to the inherited 'color'.
        FoBlock block = FirstBlock("<fo:block color=\"#123456\" border=\"1pt solid\">x</fo:block>");
        BorderEdge edge = block.Box.BorderTop;
        Assert.Equal(0x12, edge.Color.Red);
        Assert.Equal(0x34, edge.Color.Green);
        Assert.Equal(0x56, edge.Color.Blue);
    }

    [Fact]
    public void PerEdgeLonghands_OverrideShorthand()
    {
        FoBlock block = FirstBlock(
            "<fo:block border=\"1pt solid #000000\" border-top-width=\"5pt\" border-top-color=\"#ff0000\" "
            + "border-bottom-style=\"dashed\">x</fo:block>");
        BoxProperties box = block.Box;

        Assert.Equal(5_000, box.BorderTop.Width.Millipoints, 3);
        Assert.Equal(255, box.BorderTop.Color.Red);
        Assert.Equal(BorderStyle.Solid, box.BorderTop.Style);

        // bottom keeps shorthand width/color but overrides style.
        Assert.Equal(1_000, box.BorderBottom.Width.Millipoints, 3);
        Assert.Equal(BorderStyle.Dashed, box.BorderBottom.Style);
    }

    [Fact]
    public void RelativeEdges_MapToPhysicalUnderLrTb()
    {
        // before=top, after=bottom, start=left, end=right.
        FoBlock block = FirstBlock(
            "<fo:block border-before-width=\"1pt\" border-before-style=\"solid\" "
            + "border-after-width=\"2pt\" border-after-style=\"solid\" "
            + "border-start-width=\"3pt\" border-start-style=\"solid\" "
            + "border-end-width=\"4pt\" border-end-style=\"solid\">x</fo:block>");
        BoxProperties box = block.Box;
        Assert.Equal(1_000, box.BorderTop.Width.Millipoints, 3);
        Assert.Equal(2_000, box.BorderBottom.Width.Millipoints, 3);
        Assert.Equal(3_000, box.BorderLeft.Width.Millipoints, 3);
        Assert.Equal(4_000, box.BorderRight.Width.Millipoints, 3);
    }

    [Fact]
    public void BorderComponentShorthands_ExpandCssStyle()
    {
        // border-width with two values: vertical | horizontal. border-style solid all round.
        FoBlock block = FirstBlock(
            "<fo:block border-width=\"1pt 4pt\" border-style=\"solid\" border-color=\"#000000\">x</fo:block>");
        BoxProperties box = block.Box;
        Assert.Equal(1_000, box.BorderTop.Width.Millipoints, 3);
        Assert.Equal(1_000, box.BorderBottom.Width.Millipoints, 3);
        Assert.Equal(4_000, box.BorderRight.Width.Millipoints, 3);
        Assert.Equal(4_000, box.BorderLeft.Width.Millipoints, 3);
    }

    [Fact]
    public void Padding_ShorthandAndLonghands()
    {
        FoBlock block = FirstBlock(
            "<fo:block padding=\"3pt\" padding-left=\"10pt\" padding-before=\"7pt\">x</fo:block>");
        BoxProperties box = block.Box;
        Assert.Equal(7_000, box.PaddingTop.Millipoints, 3);   // before -> top, overridden
        Assert.Equal(3_000, box.PaddingRight.Millipoints, 3);
        Assert.Equal(3_000, box.PaddingBottom.Millipoints, 3);
        Assert.Equal(10_000, box.PaddingLeft.Millipoints, 3);
    }

    [Fact]
    public void ZeroWidthOrNoneStyle_EdgeNotVisible()
    {
        FoBlock zeroWidth = FirstBlock("<fo:block border-top-style=\"solid\" border-top-width=\"0pt\">x</fo:block>");
        Assert.False(zeroWidth.Box.BorderTop.IsVisible);

        FoBlock noneStyle = FirstBlock("<fo:block border-top-width=\"5pt\" border-top-style=\"none\">x</fo:block>");
        Assert.False(noneStyle.Box.BorderTop.IsVisible);
    }

    [Fact]
    public void BoxProperties_AreNotInherited()
    {
        FoBlock parent = FirstBlock(
            "<fo:block background-color=\"#ff0000\" border=\"2pt solid #000000\" padding=\"4pt\">"
            + "<fo:block>child</fo:block></fo:block>");
        FoBlock child = parent.ChildObjects.OfType<FoBlock>().Single();
        Assert.True(child.Box.IsEmpty);
        Assert.Equal(0, child.Box.PaddingTop.Millipoints);
    }

    [Fact]
    public void InsetAccessors_SumBorderAndPadding()
    {
        FoBlock block = FirstBlock(
            "<fo:block border=\"2pt solid #000000\" padding=\"3pt\">x</fo:block>");
        BoxProperties box = block.Box;
        Assert.Equal(5_000, box.LeftInsetMpt, 3);
        Assert.Equal(5_000, box.TopInsetMpt, 3);
        Assert.Equal(5_000, box.RightInsetMpt, 3);
        Assert.Equal(5_000, box.BottomInsetMpt, 3);
    }

    [Fact]
    public void BorderWidthKeyword_MapsThinMediumThick()
    {
        FoBlock block = FirstBlock(
            "<fo:block border-top=\"thin solid\" border-bottom=\"thick solid\">x</fo:block>");
        Assert.Equal(1_000, block.Box.BorderTop.Width.Millipoints, 3);
        Assert.Equal(5_000, block.Box.BorderBottom.Width.Millipoints, 3);
    }

    [Fact]
    public void ExternalGraphic_ParsesSourceAndDimensions()
    {
        FoBlock block = FirstBlock(
            "<fo:block><fo:external-graphic src=\"url('logo.png')\" content-width=\"50pt\" "
            + "content-height=\"30pt\"/></fo:block>");
        FoExternalGraphic graphic = block.ChildObjects.OfType<FoExternalGraphic>().Single();
        Assert.Equal("logo.png", graphic.Source);
        Assert.Equal(50_000, graphic.ContentWidth!.Value.Millipoints, 3);
        Assert.Equal(30_000, graphic.ContentHeight!.Value.Millipoints, 3);
    }

    [Fact]
    public void ExternalGraphic_AutoDimensionsAreNull()
    {
        FoBlock block = FirstBlock(
            "<fo:block><fo:external-graphic src=\"pic.jpg\" content-width=\"auto\"/></fo:block>");
        FoExternalGraphic graphic = block.ChildObjects.OfType<FoExternalGraphic>().Single();
        Assert.Equal("pic.jpg", graphic.Source);
        Assert.Null(graphic.ContentWidth);
        Assert.Null(graphic.ContentHeight);
    }

    [Fact]
    public void BackgroundImage_UrlResolvesWithDefaults()
    {
        FoBlock block = FirstBlock("<fo:block background-image=\"url('bg.png')\">x</fo:block>");
        BoxProperties box = block.Box;
        Assert.True(box.HasBackgroundImage);
        Assert.True(box.HasBackground);
        BackgroundImage bg = box.BackgroundImage!;
        Assert.Equal("bg.png", bg.Uri);
        Assert.Equal(BackgroundRepeat.Repeat, bg.Repeat);     // default
        Assert.Equal(BackgroundPosition.Zero, bg.PositionHorizontal);
        Assert.Equal(BackgroundPosition.Zero, bg.PositionVertical);
    }

    [Fact]
    public void BackgroundImage_NoneIsNull()
    {
        FoBlock block = FirstBlock("<fo:block background-image=\"none\">x</fo:block>");
        Assert.False(block.Box.HasBackgroundImage);
        Assert.False(block.Box.HasBackground);
    }

    [Fact]
    public void BackgroundImage_BareReferenceAccepted()
    {
        FoBlock block = FirstBlock("<fo:block background-image=\"tile.gif\">x</fo:block>");
        Assert.Equal("tile.gif", block.Box.BackgroundImage!.Uri);
    }

    [Fact]
    public void BackgroundRepeat_Parsed()
    {
        FoBlock block = FirstBlock(
            "<fo:block background-image=\"url(b.png)\" background-repeat=\"no-repeat\">x</fo:block>");
        Assert.Equal(BackgroundRepeat.NoRepeat, block.Box.BackgroundImage!.Repeat);
    }

    [Fact]
    public void BackgroundPosition_KeywordsMapToPercent()
    {
        FoBlock block = FirstBlock(
            "<fo:block background-image=\"url(b.png)\" background-position-horizontal=\"right\" "
            + "background-position-vertical=\"center\">x</fo:block>");
        BackgroundImage bg = block.Box.BackgroundImage!;
        Assert.True(bg.PositionHorizontal.IsPercent);
        Assert.Equal(1.0, bg.PositionHorizontal.Percent, 6);
        Assert.True(bg.PositionVertical.IsPercent);
        Assert.Equal(0.5, bg.PositionVertical.Percent, 6);
    }

    [Fact]
    public void BackgroundPosition_LengthAndPercent()
    {
        FoBlock block = FirstBlock(
            "<fo:block background-image=\"url(b.png)\" background-position-horizontal=\"10pt\" "
            + "background-position-vertical=\"25%\">x</fo:block>");
        BackgroundImage bg = block.Box.BackgroundImage!;
        Assert.False(bg.PositionHorizontal.IsPercent);
        Assert.Equal(10_000, bg.PositionHorizontal.LengthMpt, 3);
        Assert.True(bg.PositionVertical.IsPercent);
        Assert.Equal(0.25, bg.PositionVertical.Percent, 6);
    }

    [Fact]
    public void BackgroundPositionShorthand_SplitsAcrossAxes()
    {
        FoBlock block = FirstBlock(
            "<fo:block background-image=\"url(b.png)\" background-position=\"right top\">x</fo:block>");
        BackgroundImage bg = block.Box.BackgroundImage!;
        Assert.Equal(1.0, bg.PositionHorizontal.Percent, 6);   // right
        Assert.Equal(0.0, bg.PositionVertical.Percent, 6);     // top
    }

    [Fact]
    public void BackgroundShorthand_ExpandsImageRepeatPosition()
    {
        FoBlock block = FirstBlock(
            "<fo:block background=\"#eeeeee url('paper.png') no-repeat center\">x</fo:block>");
        BoxProperties box = block.Box;
        Assert.NotNull(box.BackgroundColor);
        Assert.Equal(0xEE, box.BackgroundColor!.Red);
        BackgroundImage bg = box.BackgroundImage!;
        Assert.Equal("paper.png", bg.Uri);
        Assert.Equal(BackgroundRepeat.NoRepeat, bg.Repeat);
        Assert.Equal(0.5, bg.PositionHorizontal.Percent, 6);   // center -> horizontal
    }

    [Fact]
    public void BackgroundImage_NotInherited()
    {
        FoBlock parent = FirstBlock(
            "<fo:block background-image=\"url(b.png)\"><fo:block>child</fo:block></fo:block>");
        FoBlock child = parent.ChildObjects.OfType<FoBlock>().Single();
        Assert.False(child.Box.HasBackgroundImage);
    }
}
