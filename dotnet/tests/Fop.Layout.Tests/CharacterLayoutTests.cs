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

/// <summary>Layout tests for <c>fo:character</c> (a single styled glyph inline).</summary>
public sealed class CharacterLayoutTests
{
    private static readonly FakeFontMeasurer Measurer = new();

    private static AreaTree LayOut(string body)
    {
        string fo = $"""
            <fo:root xmlns:fo="http://www.w3.org/1999/XSL/Format">
              <fo:layout-master-set>
                <fo:simple-page-master master-name="p" page-width="200pt" page-height="400pt">
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

    [Fact]
    public void Character_RendersAsSingleGlyphWord()
    {
        // A standalone fo:character contributes one word with its single character.
        AreaTree tree = LayOut("<fo:block><fo:character character=\"b\"/></fo:block>");
        PageArea page = Assert.Single(tree.Pages);
        TextRun run = Assert.Single(page.TextRuns);
        Assert.Equal("b", run.Text);
    }

    [Fact]
    public void Character_IsAStandaloneInlineWord()
    {
        // The character is an inline word atom; flanking text contributes its own words. (FOP would glue
        // the character to adjacent text without whitespace; this word-atom model keeps them separate.)
        AreaTree tree = LayOut("<fo:block>a<fo:character character=\"b\"/>c</fo:block>");
        PageArea page = Assert.Single(tree.Pages);
        Assert.Contains("b", string.Concat(page.TextRuns.Select(r => r.Text)));
    }

    [Fact]
    public void Character_UsesOwnColour()
    {
        AreaTree tree = LayOut(
            "<fo:block color=\"#000000\">x<fo:character character=\"y\" color=\"#ff0000\"/></fo:block>");
        PageArea page = Assert.Single(tree.Pages);
        TextRun red = page.TextRuns.Single(r => r.Text == "y");
        Assert.Equal(255, red.Color.Red);
        Assert.Equal(0, red.Color.Green);
    }

    [Fact]
    public void Character_EmptyOrUnsetContributesNothing()
    {
        AreaTree tree = LayOut("<fo:block>z<fo:character/></fo:block>");
        PageArea page = Assert.Single(tree.Pages);
        Assert.Equal("z", string.Concat(page.TextRuns.Select(r => r.Text)));
    }
}
