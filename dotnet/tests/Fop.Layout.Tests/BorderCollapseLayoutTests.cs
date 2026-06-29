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
using Fop.Layout;

using Xunit;

namespace Fop.Layout.Tests;

/// <summary>
/// Layout tests for the collapsing border model (<c>border-collapse="collapse"</c>): interior shared
/// cell edges are drawn once rather than doubled.
/// </summary>
public sealed class BorderCollapseLayoutTests
{
    private static readonly FakeFontMeasurer Measurer = new();

    private static AreaTree LayOut(string body, double pageWidthPt = 200)
    {
        string fo = $"""
            <fo:root xmlns:fo="http://www.w3.org/1999/XSL/Format">
              <fo:layout-master-set>
                <fo:simple-page-master master-name="p" page-width="{pageWidthPt}pt" page-height="800pt">
                  <fo:region-body/>
                </fo:simple-page-master>
              </fo:layout-master-set>
              <fo:page-sequence master-reference="p">
                <fo:flow flow-name="xsl-region-body">{body}</fo:flow>
              </fo:page-sequence>
            </fo:root>
            """;
        return new LayoutEngine(Measurer).LayOut(FoTreeBuilder.ParseString(fo));
    }

    private const string TwoByTwo = """
        <fo:table width="200pt" {0}>
          <fo:table-column column-width="100pt"/>
          <fo:table-column column-width="100pt"/>
          <fo:table-body>
            <fo:table-row>
              <fo:table-cell border="1pt solid #000000"><fo:block>a</fo:block></fo:table-cell>
              <fo:table-cell border="1pt solid #000000"><fo:block>b</fo:block></fo:table-cell>
            </fo:table-row>
            <fo:table-row>
              <fo:table-cell border="1pt solid #000000"><fo:block>c</fo:block></fo:table-cell>
              <fo:table-cell border="1pt solid #000000"><fo:block>d</fo:block></fo:table-cell>
            </fo:table-row>
          </fo:table-body>
        </fo:table>
        """;

    [Fact]
    public void Separate_EachCellPaintsAllFourBorders()
    {
        AreaTree tree = LayOut(string.Format(TwoByTwo, ""), pageWidthPt: 200);
        PageArea page = Assert.Single(tree.Pages);
        // 4 cells x 4 edges = 16 border RectFills (no backgrounds).
        Assert.Equal(16, page.RectFills.Count);
    }

    [Fact]
    public void Collapse_SharedInteriorEdgesPaintedOnce()
    {
        AreaTree tree = LayOut(string.Format(TwoByTwo, "border-collapse=\"collapse\""), pageWidthPt: 200);
        PageArea page = Assert.Single(tree.Pages);

        // top-left cell: top+left (2); top-right: top+left+right (3); bottom-left: top+left+bottom (3);
        // bottom-right: top+left+right+bottom (4). Total 12 < the 16 of the separate model.
        Assert.Equal(12, page.RectFills.Count);
    }

    [Fact]
    public void Collapse_OuterEdgesStillPresent()
    {
        AreaTree tree = LayOut(string.Format(TwoByTwo, "border-collapse=\"collapse\""), pageWidthPt: 200);
        PageArea page = Assert.Single(tree.Pages);

        // The bottom-right corner cell paints both the outer right and outer bottom edges.
        // Outer right edge: a 1pt-wide rect at the table's right (x ~ 200000 - 1000).
        Assert.Contains(page.RectFills, r => r.WidthMpt <= 1_001 && r.XMpt >= 198_999);
        // Outer bottom edge: a 1pt-tall rect at the table bottom.
        double maxBottom = page.RectFills.Max(r => r.YMpt + r.HeightMpt);
        Assert.Contains(page.RectFills, r => r.HeightMpt <= 1_001 && r.YMpt + r.HeightMpt >= maxBottom - 1);
    }
}
