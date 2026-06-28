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

namespace Fop.Fo.Tests;

/// <summary>Parsing + property tests for the <c>fo:float</c> formatting object.</summary>
public sealed class FloatPropertyTests
{
    private static FoFloat ParseFloat(string floatMarkup)
    {
        string fo = $"""
            <fo:root xmlns:fo="http://www.w3.org/1999/XSL/Format">
              <fo:layout-master-set>
                <fo:simple-page-master master-name="p" page-width="400pt" page-height="400pt">
                  <fo:region-body/>
                </fo:simple-page-master>
              </fo:layout-master-set>
              <fo:page-sequence master-reference="p">
                <fo:flow flow-name="xsl-region-body">
                  {floatMarkup}
                </fo:flow>
              </fo:page-sequence>
            </fo:root>
            """;
        FoRoot root = FoTreeBuilder.ParseString(fo);
        return root.PageSequences.First().Flow!.ChildObjects.OfType<FoFloat>().First();
    }

    [Theory]
    [InlineData("before", FloatKind.Before)]
    [InlineData("none", FloatKind.None)]
    [InlineData("left", FloatKind.Start)]
    [InlineData("start", FloatKind.Start)]
    [InlineData("right", FloatKind.End)]
    [InlineData("end", FloatKind.End)]
    public void ResolvesFloatKeyword(string keyword, FloatKind expected)
    {
        FoFloat floatFo = ParseFloat($"<fo:float float=\"{keyword}\"><fo:block>x</fo:block></fo:float>");
        Assert.Equal(expected, floatFo.Float);
    }

    [Fact]
    public void DefaultsToNoneWhenUnset()
    {
        FoFloat floatFo = ParseFloat("<fo:float><fo:block>x</fo:block></fo:float>");
        Assert.Equal(FloatKind.None, floatFo.Float);
    }

    [Fact]
    public void ExposesBlockLevelChildren()
    {
        FoFloat floatFo = ParseFloat("""
            <fo:float float="before">
              <fo:block>one</fo:block>
              <fo:block>two</fo:block>
            </fo:float>
            """);
        Assert.Equal(2, floatFo.BlockLevelChildren.OfType<FoBlock>().Count());
    }
}
