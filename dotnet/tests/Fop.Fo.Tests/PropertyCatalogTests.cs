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

using System.Collections.Generic;

using Fop.Fo;

using Xunit;

namespace Fop.Fo.Tests;

public class PropertyCatalogTests
{
    [Theory]
    [InlineData("color", true)]
    [InlineData("font-size", true)]
    [InlineData("font-family", true)]
    [InlineData("line-height", true)]
    [InlineData("border-collapse", true)]
    [InlineData("widows", true)]
    [InlineData("background-color", false)]
    [InlineData("border", false)]
    [InlineData("padding", false)]
    [InlineData("break-before", false)]
    [InlineData("absolute-position", false)]
    public void Inheritance_MatchesFopMapping(string property, bool inherited)
    {
        Assert.Equal(inherited, PropertyCatalog.IsInherited(property));
    }

    [Fact]
    public void CompoundSubProperty_ResolvesToBaseProperty()
    {
        // keep-together inherits in the catalogue; its .within-page component shares that flag.
        Assert.True(PropertyCatalog.IsInherited("keep-together.within-page"));
        // space-before is not inherited; .optimum follows the base.
        Assert.False(PropertyCatalog.IsInherited("space-before.optimum"));
        Assert.True(PropertyCatalog.IsKnown("space-before.optimum"));
    }

    [Fact]
    public void IsKnown_TrueForRecognisedProperties_FalseOtherwise()
    {
        Assert.True(PropertyCatalog.IsKnown("text-align"));
        Assert.True(PropertyCatalog.IsKnown("flow-name"));
        Assert.False(PropertyCatalog.IsKnown("not-a-real-property"));
    }

    [Fact]
    public void Catalog_CoversTheFullPropertySet()
    {
        // The transcription should cover the ~265 FOP properties (a regression guard on the data).
        Assert.True(PropertyCatalog.All.Count >= 260, $"only {PropertyCatalog.All.Count} properties");
    }

    [Fact]
    public void EnumProperty_CarriesItsValueSet()
    {
        PropertyDef? textAlign = PropertyCatalog.Lookup("text-align");
        Assert.NotNull(textAlign);
        Assert.Equal(PropertyDatatype.Enum, textAlign!.Datatype);
        Assert.Contains("center", textAlign.EnumValues);
        Assert.Contains("justify", textAlign.EnumValues);
    }
}

public class PropertyValidatorTests
{
    private static IReadOnlyList<PropertyValidationMessage> Validate(params (string, string)[] props)
    {
        var dict = new Dictionary<string, string>();
        foreach ((string k, string v) in props)
        {
            dict[k] = v;
        }

        return PropertyValidator.Validate(new PropertyList(dict, parent: null));
    }

    [Fact]
    public void UnknownProperty_Warns()
    {
        var messages = Validate(("colr", "red"));
        PropertyValidationMessage m = Assert.Single(messages);
        Assert.Equal(ValidationSeverity.Warning, m.Severity);
        Assert.Equal("colr", m.PropertyName);
    }

    [Fact]
    public void InvalidEnumValue_Errors()
    {
        var messages = Validate(("text-align", "middle"));
        PropertyValidationMessage m = Assert.Single(messages);
        Assert.Equal(ValidationSeverity.Error, m.Severity);
        Assert.Contains("text-align", m.Message);
    }

    [Fact]
    public void ValidEnumValue_NoMessage()
    {
        Assert.Empty(Validate(("text-align", "center"), ("border-collapse", "collapse")));
    }

    [Fact]
    public void MalformedLength_Errors()
    {
        var messages = Validate(("width", "abc"));
        Assert.Equal(ValidationSeverity.Error, Assert.Single(messages).Severity);
    }

    [Theory]
    [InlineData("width", "10pt")]
    [InlineData("width", "50%")]
    [InlineData("width", "auto")]
    [InlineData("margin-left", "1.5cm")]
    [InlineData("border-top-width", "thin")]
    public void ValidLength_NoMessage(string name, string value)
    {
        Assert.Empty(Validate((name, value)));
    }

    [Fact]
    public void InvalidColour_Errors()
    {
        var messages = Validate(("background-color", "not-a-colour"));
        Assert.Equal(ValidationSeverity.Error, Assert.Single(messages).Severity);
    }

    [Theory]
    [InlineData("background-color", "#ff0000")]
    [InlineData("background-color", "transparent")]
    [InlineData("color", "blue")]
    public void ValidColour_NoMessage(string name, string value)
    {
        Assert.Empty(Validate((name, value)));
    }

    [Fact]
    public void InheritKeyword_AlwaysAccepted()
    {
        Assert.Empty(Validate(("text-align", "inherit"), ("width", "inherit")));
    }

    [Fact]
    public void Expression_NotRejected()
    {
        // A property expression resolves dynamically; it is not value-checked statically.
        Assert.Empty(Validate(("width", "3pt + 2pt")));
    }

    [Fact]
    public void CompoundComponent_RecognisedNotUnknown()
    {
        // A compound sub-property resolves to its base property's definition, so it is not flagged as an
        // unknown property. (space-before is a Space datatype, whose grammar is accepted leniently.)
        Assert.Empty(Validate(("space-before.optimum", "12pt")));
    }

    [Fact]
    public void NamespacedAttributes_Ignored()
    {
        // xml:lang / fox:* etc. are not validated against the core catalogue.
        Assert.Empty(Validate(("xml:lang", "en"), ("fox:foo", "bar")));
    }

    [Fact]
    public void ValidateTree_FindsIssuesAcrossTheDocument()
    {
        string fo = """
            <fo:root xmlns:fo="http://www.w3.org/1999/XSL/Format">
              <fo:layout-master-set>
                <fo:simple-page-master master-name="p" page-width="200pt" page-height="200pt">
                  <fo:region-body/>
                </fo:simple-page-master>
              </fo:layout-master-set>
              <fo:page-sequence master-reference="p">
                <fo:flow flow-name="xsl-region-body">
                  <fo:block text-align="middle">bad enum</fo:block>
                  <fo:block width="oops">bad length</fo:block>
                </fo:flow>
              </fo:page-sequence>
            </fo:root>
            """;
        FoRoot root = FoTreeBuilder.ParseString(fo);
        var messages = PropertyValidator.ValidateTree(root);

        Assert.Contains(messages, m => m.PropertyName == "text-align" && m.Severity == ValidationSeverity.Error);
        Assert.Contains(messages, m => m.PropertyName == "width" && m.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void ValidateTree_CleanDocumentHasNoMessages()
    {
        string fo = """
            <fo:root xmlns:fo="http://www.w3.org/1999/XSL/Format">
              <fo:layout-master-set>
                <fo:simple-page-master master-name="p" page-width="200pt" page-height="200pt">
                  <fo:region-body/>
                </fo:simple-page-master>
              </fo:layout-master-set>
              <fo:page-sequence master-reference="p">
                <fo:flow flow-name="xsl-region-body">
                  <fo:block text-align="center" font-size="12pt" color="#112233">ok</fo:block>
                </fo:flow>
              </fo:page-sequence>
            </fo:root>
            """;
        FoRoot root = FoTreeBuilder.ParseString(fo);
        Assert.Empty(PropertyValidator.ValidateTree(root));
    }
}
