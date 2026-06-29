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

namespace Fop.Fo;

/// <summary>The broad datatype family of an XSL-FO property, used to drive validation.</summary>
public enum PropertyDatatype
{
    /// <summary>An enumerated keyword from a fixed set (see <see cref="PropertyDef.EnumValues"/>).</summary>
    Enum,

    /// <summary>A length (possibly a percentage or the <c>auto</c>/keyword forms).</summary>
    Length,

    /// <summary>A colour (named, <c>#hex</c>, <c>rgb()</c>, <c>transparent</c>, ...).</summary>
    Color,

    /// <summary>A number (may be fractional).</summary>
    Number,

    /// <summary>A string (free text; not validated).</summary>
    String,

    /// <summary>A single character.</summary>
    Character,

    /// <summary>A conditional length (<c>length [discard|retain]</c>).</summary>
    CondLength,

    /// <summary>A space specifier (min/opt/max + conditionality).</summary>
    Space,

    /// <summary>A length-range (min/opt/max lengths).</summary>
    LengthRange,

    /// <summary>A length pair (e.g. <c>border-spacing</c>).</summary>
    LengthPair,

    /// <summary>A keep specifier (<c>auto</c>/<c>always</c>/integer).</summary>
    Keep,

    /// <summary>A shorthand expanded into longhands (validated as its components).</summary>
    Shorthand,

    /// <summary>A URI specification (<c>url(...)</c> or a bare reference).</summary>
    Uri,

    /// <summary>A space-separated list value (e.g. the <c>border</c> shorthand).</summary>
    List,

    /// <summary>A font-family list.</summary>
    FontFamily,

    /// <summary>A property recognised by FOP but not implemented (no value validation performed).</summary>
    Stub,

    /// <summary>An unclassified property (no value validation performed).</summary>
    Unknown,
}

/// <summary>
/// The static definition of one XSL-FO property: whether it inherits, its datatype family, the valid
/// keyword set (for an enumerated property) and its initial (default) value. Mirrors the per-property
/// configuration FOP builds in <c>FOPropertyMapping</c> (<c>setInherited</c>/<c>addEnum</c>/
/// <c>setDefault</c>).
/// </summary>
/// <param name="Name">The property name (as written in XSL-FO).</param>
/// <param name="Inherited">Whether the property inherits from the parent formatting object.</param>
/// <param name="Datatype">The datatype family.</param>
/// <param name="EnumValues">The valid keyword set for an <see cref="PropertyDatatype.Enum"/> property.</param>
/// <param name="Default">The initial value, or <c>null</c> when none is declared.</param>
public sealed record PropertyDef(
    string Name,
    bool Inherited,
    PropertyDatatype Datatype,
    IReadOnlySet<string> EnumValues,
    string? Default);

/// <summary>
/// The catalogue of the XSL-FO properties FOP recognises (~265), each with its inheritance flag,
/// datatype family, enumerated value set and initial value. This is the reference model behind the
/// property subsystem: <see cref="PropertyList"/> consults <see cref="IsInherited"/> for the
/// inheritance cascade, and <see cref="PropertyValidator"/> uses the datatype/enum data to flag
/// malformed declarations.
/// <para>
/// The data is transcribed from <c>org.apache.fop.fo.FOPropertyMapping</c>. Compound sub-properties
/// (e.g. <c>space-before.optimum</c>) are not listed individually; a property name carrying a
/// recognised <c>.component</c> suffix is validated against its base property.
/// </para>
/// </summary>
public static class PropertyCatalog
{
    private static readonly IReadOnlySet<string> NoEnum = new HashSet<string>(0);

    private static PropertyDef Def(string name, bool inherited, PropertyDatatype datatype, string? @default,
        params string[] enumValues)
        => new(name, inherited, datatype,
            enumValues.Length == 0 ? NoEnum : new HashSet<string>(enumValues, StringComparer.Ordinal),
            @default);

    private static readonly PropertyDef[] Definitions =
    [
        // Length properties declared in FOPropertyMapping via shared maker variables (top/right/bottom/
        // left, content/page dimensions, border widths, column-gap).
        Def("top", false, PropertyDatatype.Length, "auto"),
        Def("right", false, PropertyDatatype.Length, "auto"),
        Def("bottom", false, PropertyDatatype.Length, "auto"),
        Def("left", false, PropertyDatatype.Length, "auto"),
        Def("width", false, PropertyDatatype.Length, "auto"),
        Def("height", false, PropertyDatatype.Length, "auto"),
        Def("content-width", false, PropertyDatatype.Length, "auto"),
        Def("content-height", false, PropertyDatatype.Length, "auto"),
        Def("page-width", false, PropertyDatatype.Length, "auto"),
        Def("page-height", false, PropertyDatatype.Length, "auto"),
        Def("column-gap", false, PropertyDatatype.Length, "0.25in"),
        Def("border-top-width", false, PropertyDatatype.CondLength, "medium"),
        Def("border-bottom-width", false, PropertyDatatype.CondLength, "medium"),
        Def("border-left-width", false, PropertyDatatype.CondLength, "medium"),
        Def("border-right-width", false, PropertyDatatype.CondLength, "medium"),

        Def("absolute-position", false, PropertyDatatype.Enum, "auto", "absolute", "auto", "fixed"),
        Def("active-state", false, PropertyDatatype.Stub, ""),
        Def("alignment-adjust", false, PropertyDatatype.Length, "auto"),
        Def("alignment-baseline", false, PropertyDatatype.Enum, "auto", "after-edge", "alphabetic", "auto", "baseline", "before-edge", "central", "hanging", "ideographic", "mathematical", "middle", "text-after-edge", "text-before-edge"),
        Def("auto-restore", true, PropertyDatatype.Stub, "false"),
        Def("azimuth", true, PropertyDatatype.Stub, "center"),
        Def("background", false, PropertyDatatype.Stub, "none"),
        Def("background-attachment", false, PropertyDatatype.Enum, "scroll", "fixed", "scroll"),
        Def("background-color", false, PropertyDatatype.Color, "transparent"),
        Def("background-image", false, PropertyDatatype.String, "none"),
        Def("background-position", false, PropertyDatatype.Shorthand, "0pt 0pt"),
        Def("background-position-horizontal", false, PropertyDatatype.Length, "0pt"),
        Def("background-position-vertical", false, PropertyDatatype.Length, "0pt"),
        Def("background-repeat", false, PropertyDatatype.Enum, "repeat", "no-repeat", "repeat", "repeat-x", "repeat-y"),
        Def("baseline-shift", false, PropertyDatatype.Length, "baseline"),
        Def("blank-or-not-blank", false, PropertyDatatype.Enum, "any", "any", "blank", "not-blank"),
        Def("block-progression-dimension", false, PropertyDatatype.Length, "auto"),
        Def("border", false, PropertyDatatype.List, ""),
        Def("border-after-color", false, PropertyDatatype.Color, "black"),
        Def("border-after-precedence", false, PropertyDatatype.Unknown, null),
        Def("border-after-style", false, PropertyDatatype.Enum, null),
        Def("border-after-width", false, PropertyDatatype.CondLength, "discard"),
        Def("border-before-color", false, PropertyDatatype.Color, "black"),
        Def("border-before-precedence", false, PropertyDatatype.Unknown, null),
        Def("border-before-style", false, PropertyDatatype.Enum, null),
        Def("border-before-width", false, PropertyDatatype.CondLength, "discard"),
        Def("border-bottom", false, PropertyDatatype.List, ""),
        Def("border-bottom-color", false, PropertyDatatype.Color, "black"),
        Def("border-bottom-style", false, PropertyDatatype.Enum, null),
        Def("border-collapse", true, PropertyDatatype.Enum, "collapse", "collapse", "collapse-with-precedence", "separate"),
        Def("border-color", false, PropertyDatatype.List, ""),
        Def("border-end-color", false, PropertyDatatype.Color, "black"),
        Def("border-end-precedence", false, PropertyDatatype.Unknown, null),
        Def("border-end-style", false, PropertyDatatype.Enum, null),
        Def("border-end-width", false, PropertyDatatype.CondLength, "discard"),
        Def("border-left", false, PropertyDatatype.List, ""),
        Def("border-left-color", false, PropertyDatatype.Color, "black"),
        Def("border-left-style", false, PropertyDatatype.Enum, null),
        Def("border-right", false, PropertyDatatype.List, ""),
        Def("border-right-color", false, PropertyDatatype.Color, "black"),
        Def("border-right-style", false, PropertyDatatype.Enum, null),
        Def("border-separation", true, PropertyDatatype.Length, "0pt"),
        Def("border-spacing", true, PropertyDatatype.List, "0pt"),
        Def("border-start-color", false, PropertyDatatype.Color, "black"),
        Def("border-start-precedence", false, PropertyDatatype.Unknown, null),
        Def("border-start-style", false, PropertyDatatype.Enum, null),
        Def("border-start-width", false, PropertyDatatype.CondLength, "discard"),
        Def("border-style", false, PropertyDatatype.List, ""),
        Def("border-top", false, PropertyDatatype.List, ""),
        Def("border-top-color", false, PropertyDatatype.Color, "black"),
        Def("border-top-style", false, PropertyDatatype.Enum, null),
        Def("border-width", false, PropertyDatatype.List, ""),
        Def("break-after", false, PropertyDatatype.Enum, null),
        Def("break-before", false, PropertyDatatype.Enum, null),
        Def("caption-side", true, PropertyDatatype.Enum, "before", "after", "before", "bottom", "end", "left", "right", "start", "top"),
        Def("case-name", false, PropertyDatatype.Stub, ""),
        Def("case-title", false, PropertyDatatype.Stub, ""),
        Def("change-bar-class", false, PropertyDatatype.String, ""),
        Def("change-bar-color", true, PropertyDatatype.Color, "black"),
        Def("change-bar-offset", true, PropertyDatatype.Length, "6pt"),
        Def("change-bar-placement", true, PropertyDatatype.Enum, "start", "alternate", "end", "inside", "left", "outside", "right", "start"),
        Def("change-bar-style", true, PropertyDatatype.Enum, "solid"),
        Def("change-bar-width", true, PropertyDatatype.Length, "6pt"),
        Def("character", false, PropertyDatatype.Character, "none"),
        Def("clear", false, PropertyDatatype.Enum, "none", "both", "end", "left", "none", "right", "start"),
        Def("clip", false, PropertyDatatype.Stub, "auto"),
        Def("color", true, PropertyDatatype.Color, "black"),
        Def("color-profile-name", false, PropertyDatatype.String, ""),
        Def("column-count", false, PropertyDatatype.Unknown, "1"),
        Def("column-number", false, PropertyDatatype.Unknown, null),
        Def("column-width", false, PropertyDatatype.Length, "auto"),
        Def("content-type", false, PropertyDatatype.String, "auto"),
        Def("country", true, PropertyDatatype.String, "none"),
        Def("cue", false, PropertyDatatype.Stub, ""),
        Def("cue-after", false, PropertyDatatype.Stub, "none"),
        Def("cue-before", false, PropertyDatatype.Stub, "none"),
        Def("destination-placement-offset", false, PropertyDatatype.Stub, "0pt"),
        Def("direction", true, PropertyDatatype.Enum, "ltr", "ltr", "rtl"),
        Def("display-align", true, PropertyDatatype.Enum, "auto", "after", "auto", "before", "center"),
        Def("dominant-baseline", false, PropertyDatatype.Enum, "auto", "alphabetic", "auto", "central", "hanging", "ideographic", "mathematical", "middle", "no-change", "reset-size", "text-after-edge", "text-before-edge", "use-script"),
        Def("elevation", true, PropertyDatatype.Stub, "level"),
        Def("empty-cells", true, PropertyDatatype.Enum, "show", "hide", "show"),
        Def("end-indent", true, PropertyDatatype.Length, "0pt"),
        Def("ends-row", false, PropertyDatatype.Enum, "false"),
        Def("extent", true, PropertyDatatype.Length, "0pt"),
        Def("external-destination", false, PropertyDatatype.String, ""),
        Def("float", false, PropertyDatatype.Enum, "none", "before", "end", "left", "none", "right", "start"),
        Def("flow-name", false, PropertyDatatype.String, ""),
        Def("font", true, PropertyDatatype.Shorthand, ""),
        Def("font-family", true, PropertyDatatype.FontFamily, "sans-serif,Symbol,ZapfDingbats"),
        Def("font-selection-strategy", true, PropertyDatatype.Enum, "auto", "auto", "character-by-character"),
        Def("font-size", true, PropertyDatatype.Unknown, "12pt"),
        Def("font-size-adjust", true, PropertyDatatype.Number, "none"),
        Def("font-stretch", false, PropertyDatatype.Unknown, "normal"),
        Def("font-style", true, PropertyDatatype.Enum, "normal", "backslant", "italic", "normal", "oblique"),
        Def("font-variant", true, PropertyDatatype.Enum, "normal", "normal", "small-caps"),
        Def("font-weight", true, PropertyDatatype.Unknown, "400"),
        Def("force-page-count", false, PropertyDatatype.Enum, "auto", "auto", "doubly-even", "doubly-odd", "end-on-doubly-even", "end-on-doubly-odd", "end-on-even", "end-on-odd", "even", "no-force", "odd"),
        Def("format", false, PropertyDatatype.String, "1"),
        Def("fox:abbreviation", false, PropertyDatatype.String, ""),
        Def("fox:alt-text", false, PropertyDatatype.String, ""),
        Def("fox:auto-toggle", false, PropertyDatatype.Enum, "select-first-fitting", "select-first-fitting"),
        Def("fox:background-image-height", false, PropertyDatatype.Length, "0pt"),
        Def("fox:background-image-width", false, PropertyDatatype.Length, "0pt"),
        Def("fox:border-after-end-radius", false, PropertyDatatype.List, null),
        Def("fox:border-after-radius-end", false, PropertyDatatype.CondLength, "discard"),
        Def("fox:border-after-radius-start", false, PropertyDatatype.CondLength, "discard"),
        Def("fox:border-after-start-radius", false, PropertyDatatype.List, null),
        Def("fox:border-before-end-radius", false, PropertyDatatype.List, null),
        Def("fox:border-before-radius-end", false, PropertyDatatype.CondLength, "discard"),
        Def("fox:border-before-radius-start", false, PropertyDatatype.CondLength, "discard"),
        Def("fox:border-before-start-radius", false, PropertyDatatype.List, null),
        Def("fox:border-end-radius-after", false, PropertyDatatype.CondLength, "discard"),
        Def("fox:border-end-radius-before", false, PropertyDatatype.CondLength, "discard"),
        Def("fox:border-radius", false, PropertyDatatype.List, null),
        Def("fox:border-start-radius-after", false, PropertyDatatype.CondLength, "discard"),
        Def("fox:border-start-radius-before", false, PropertyDatatype.CondLength, "discard"),
        Def("fox:disable-column-balancing", true, PropertyDatatype.Enum, "false"),
        Def("fox:header", false, PropertyDatatype.Enum, "false"),
        Def("fox:layer", false, PropertyDatatype.String, ""),
        Def("fox:number-conversion-features", false, PropertyDatatype.String, ""),
        Def("fox:orphan-content-limit", true, PropertyDatatype.Length, "0pt"),
        Def("fox:widow-content-limit", true, PropertyDatatype.Length, "0pt"),
        Def("glyph-orientation-horizontal", true, PropertyDatatype.Stub, "0deg"),
        Def("glyph-orientation-vertical", true, PropertyDatatype.Stub, "auto"),
        Def("grouping-separator", false, PropertyDatatype.Character, "none"),
        Def("grouping-size", false, PropertyDatatype.Number, "0"),
        Def("hyphenate", true, PropertyDatatype.Enum, "false"),
        Def("hyphenation-character", true, PropertyDatatype.Character, "-"),
        Def("hyphenation-keep", true, PropertyDatatype.Enum, "auto", "auto", "column", "page"),
        Def("hyphenation-ladder-count", true, PropertyDatatype.Number, "no-limit"),
        Def("hyphenation-push-character-count", true, PropertyDatatype.Unknown, "2"),
        Def("hyphenation-remain-character-count", true, PropertyDatatype.Unknown, "2"),
        Def("id", false, PropertyDatatype.String, ""),
        Def("indicate-destination", false, PropertyDatatype.Stub, "false"),
        Def("initial-page-number", false, PropertyDatatype.Unknown, "auto"),
        Def("inline-progression-dimension", false, PropertyDatatype.Length, "auto"),
        Def("internal-destination", false, PropertyDatatype.String, ""),
        Def("intrusion-displace", false, PropertyDatatype.Enum, "none", "auto", "block", "indent", "line", "none"),
        Def("keep-together", true, PropertyDatatype.Keep, "auto"),
        Def("keep-with-next", false, PropertyDatatype.Keep, "auto"),
        Def("keep-with-previous", false, PropertyDatatype.Keep, "auto"),
        Def("language", true, PropertyDatatype.String, "none"),
        Def("last-line-end-indent", true, PropertyDatatype.Length, "0pt"),
        Def("leader-alignment", true, PropertyDatatype.Enum, "none", "none", "page", "reference-area"),
        Def("leader-length", true, PropertyDatatype.Length, "100%"),
        Def("leader-pattern", true, PropertyDatatype.Enum, "space", "dots", "rule", "space", "use-content"),
        Def("leader-pattern-width", true, PropertyDatatype.Length, "use-font-metrics"),
        Def("letter-spacing", true, PropertyDatatype.Unknown, "normal"),
        Def("letter-value", false, PropertyDatatype.Enum, "auto", "alphabetic", "auto", "traditional"),
        Def("line-height", true, PropertyDatatype.Unknown, "normal"),
        Def("line-height-shift-adjustment", true, PropertyDatatype.Enum, "consider-shifts", "consider-shifts", "disregard-shifts"),
        Def("line-stacking-strategy", true, PropertyDatatype.Enum, "max-height", "font-height", "line-height", "max-height"),
        Def("linefeed-treatment", true, PropertyDatatype.Enum, "treat-as-space", "ignore", "preserve", "treat-as-space", "treat-as-zero-width-space"),
        Def("margin", false, PropertyDatatype.List, ""),
        Def("margin-bottom", false, PropertyDatatype.Length, "0pt"),
        Def("margin-left", false, PropertyDatatype.Length, "0pt"),
        Def("margin-right", false, PropertyDatatype.Length, "0pt"),
        Def("margin-top", false, PropertyDatatype.Length, "0pt"),
        Def("marker-class-name", false, PropertyDatatype.String, ""),
        Def("master-name", false, PropertyDatatype.String, ""),
        Def("master-reference", false, PropertyDatatype.String, ""),
        Def("max-height", false, PropertyDatatype.Length, "0pt"),
        Def("max-width", false, PropertyDatatype.Length, "none"),
        Def("maximum-repeats", false, PropertyDatatype.Number, "no-limit"),
        Def("media-usage", false, PropertyDatatype.Enum, "auto", "auto", "bounded-in-one-dimension", "paginate", "unbounded"),
        Def("min-height", false, PropertyDatatype.Length, "0pt"),
        Def("min-width", false, PropertyDatatype.Length, ""),
        Def("number-columns-repeated", false, PropertyDatatype.Unknown, "1"),
        Def("number-columns-spanned", false, PropertyDatatype.Unknown, "1"),
        Def("number-rows-spanned", false, PropertyDatatype.Unknown, "1"),
        Def("odd-or-even", false, PropertyDatatype.Enum, "any", "any", "even", "odd"),
        Def("orphans", true, PropertyDatatype.Number, "2"),
        Def("overflow", false, PropertyDatatype.Enum, "auto", "auto", "error-if-overflow", "hidden", "scroll", "visible"),
        Def("padding", false, PropertyDatatype.List, null),
        Def("padding-after", false, PropertyDatatype.CondLength, "discard"),
        Def("padding-before", false, PropertyDatatype.CondLength, "discard"),
        Def("padding-bottom", false, PropertyDatatype.Length, null),
        Def("padding-end", false, PropertyDatatype.CondLength, "discard"),
        Def("padding-left", false, PropertyDatatype.Length, null),
        Def("padding-right", false, PropertyDatatype.Length, null),
        Def("padding-start", false, PropertyDatatype.CondLength, "discard"),
        Def("padding-top", false, PropertyDatatype.Length, null),
        Def("page-break-after", false, PropertyDatatype.Enum, "auto", "always", "auto", "avoid", "left", "right"),
        Def("page-break-before", false, PropertyDatatype.Enum, "auto", "always", "auto", "avoid", "left", "right"),
        Def("page-break-inside", true, PropertyDatatype.Enum, "auto", "auto", "avoid"),
        Def("page-position", false, PropertyDatatype.Enum, "any", "any", "auto", "first", "indefinite", "last", "only", "rest"),
        Def("pause", false, PropertyDatatype.Stub, ""),
        Def("pause-after", false, PropertyDatatype.Stub, ""),
        Def("pause-before", false, PropertyDatatype.Stub, ""),
        Def("pitch", true, PropertyDatatype.Stub, "medium"),
        Def("pitch-range", true, PropertyDatatype.Stub, "50"),
        Def("play-during", false, PropertyDatatype.Stub, "auto"),
        Def("position", false, PropertyDatatype.Enum, "static", "absolute", "fixed", "relative", "static"),
        Def("precedence", false, PropertyDatatype.Enum, "false", "auto", "indefinite"),
        Def("provisional-distance-between-starts", true, PropertyDatatype.Length, "24pt"),
        Def("provisional-label-separation", true, PropertyDatatype.Length, "6pt"),
        Def("ref-id", false, PropertyDatatype.String, ""),
        Def("reference-orientation", true, PropertyDatatype.Unknown, "0"),
        Def("region-name", false, PropertyDatatype.String, ""),
        Def("relative-align", true, PropertyDatatype.Enum, "before", "baseline", "before"),
        Def("relative-position", false, PropertyDatatype.Enum, "static", "relative", "static"),
        Def("rendering-intent", false, PropertyDatatype.Enum, "auto", "absolute-colorimetric", "auto", "perceptual", "relative-colorimetric", "saturation"),
        Def("retrieve-boundary", false, PropertyDatatype.Enum, "page-sequence", "document", "page", "page-sequence"),
        Def("retrieve-boundary-within-table", false, PropertyDatatype.Enum, "table", "page", "table", "table-fragment"),
        Def("retrieve-class-name", false, PropertyDatatype.String, ""),
        Def("retrieve-position", false, PropertyDatatype.Enum, "first-starting-within-page", "first-including-carryover", "first-starting-within-page", "last-ending-within-page", "last-starting-within-page"),
        Def("retrieve-position-within-table", false, PropertyDatatype.Enum, "first-starting", "first-including-carryover", "first-starting", "last-ending", "last-starting"),
        Def("richness", true, PropertyDatatype.Stub, "50"),
        Def("role", false, PropertyDatatype.String, "none"),
        Def("rule-style", true, PropertyDatatype.Enum, "solid", "dashed", "dotted", "double", "groove", "none", "ridge", "solid"),
        Def("rule-thickness", true, PropertyDatatype.Length, "1.0pt"),
        Def("scaling", true, PropertyDatatype.Enum, "uniform", "non-uniform", "uniform"),
        Def("scaling-method", false, PropertyDatatype.Enum, "auto", "auto", "integer-pixels", "resample-any-method"),
        Def("score-spaces", true, PropertyDatatype.Enum, "true"),
        Def("script", true, PropertyDatatype.String, "auto"),
        Def("show-destination", false, PropertyDatatype.Enum, "replace", "new", "replace"),
        Def("size", false, PropertyDatatype.Stub, "auto"),
        Def("source-document", false, PropertyDatatype.String, "none"),
        Def("space-after", false, PropertyDatatype.Space, null),
        Def("space-before", false, PropertyDatatype.Space, null),
        Def("space-end", false, PropertyDatatype.Space, null),
        Def("space-start", false, PropertyDatatype.Space, null),
        Def("span", false, PropertyDatatype.Enum, "none", "all", "none"),
        Def("speak", true, PropertyDatatype.Stub, "normal"),
        Def("speak-header", true, PropertyDatatype.Stub, "once"),
        Def("speak-numeral", true, PropertyDatatype.Stub, "continuous"),
        Def("speak-punctuation", true, PropertyDatatype.Stub, "none"),
        Def("speech-rate", true, PropertyDatatype.Stub, "medium"),
        Def("src", false, PropertyDatatype.Uri, ""),
        Def("start-indent", true, PropertyDatatype.Length, "0pt"),
        Def("starting-state", false, PropertyDatatype.Enum, "show", "hide", "show"),
        Def("starts-row", false, PropertyDatatype.Enum, "false"),
        Def("stress", true, PropertyDatatype.Stub, "50"),
        Def("suppress-at-line-break", false, PropertyDatatype.Enum, "auto", "auto", "retain", "suppress"),
        Def("switch-to", false, PropertyDatatype.String, "xsl-any"),
        Def("table-layout", false, PropertyDatatype.Enum, "auto", "auto", "fixed"),
        Def("table-omit-footer-at-break", false, PropertyDatatype.Enum, "false"),
        Def("table-omit-header-at-break", false, PropertyDatatype.Enum, "false"),
        Def("target-presentation-context", false, PropertyDatatype.Stub, "use-target-processing-context"),
        Def("target-processing-context", false, PropertyDatatype.Stub, "document-root"),
        Def("target-stylesheet", false, PropertyDatatype.Stub, "use-normal-stylesheet"),
        Def("text-align", true, PropertyDatatype.Enum, "start", "center", "end", "inside", "justify", "left", "outside", "right", "start"),
        Def("text-align-last", false, PropertyDatatype.Enum, "relative", "center", "end", "inside", "justify", "left", "outside", "relative", "right", "start"),
        Def("text-altitude", false, PropertyDatatype.Length, "use-font-metrics"),
        Def("text-decoration", false, PropertyDatatype.Enum, "none", "blink", "line-through", "no-blink", "no-line-through", "no-overline", "no-underline", "none", "overline", "underline"),
        Def("text-depth", false, PropertyDatatype.Length, "use-font-metrics"),
        Def("text-indent", true, PropertyDatatype.Length, "0pt"),
        Def("text-shadow", false, PropertyDatatype.Stub, "none"),
        Def("text-transform", true, PropertyDatatype.Enum, "none", "capitalize", "lowercase", "none", "uppercase"),
        Def("treat-as-word-space", false, PropertyDatatype.Enum, "auto", "auto"),
        Def("unicode-bidi", false, PropertyDatatype.Enum, "normal", "bidi-override", "embed", "normal"),
        Def("vertical-align", false, PropertyDatatype.Length, "baseline"),
        Def("visibility", false, PropertyDatatype.Enum, "visible", "collapse", "hidden", "visible"),
        Def("voice-family", true, PropertyDatatype.Stub, ""),
        Def("volume", true, PropertyDatatype.Stub, "medium"),
        Def("white-space", true, PropertyDatatype.Enum, "normal", "normal", "nowrap", "pre"),
        Def("white-space-collapse", true, PropertyDatatype.Enum, "true"),
        Def("white-space-treatment", true, PropertyDatatype.Enum, "ignore-if-surrounding-linefeed", "ignore", "ignore-if-after-linefeed", "ignore-if-before-linefeed", "ignore-if-surrounding-linefeed", "preserve"),
        Def("widows", true, PropertyDatatype.Number, "2"),
        Def("word-spacing", true, PropertyDatatype.Unknown, "normal"),
        Def("wrap-option", true, PropertyDatatype.Enum, "wrap", "no-wrap", "wrap"),
        Def("writing-mode", true, PropertyDatatype.Enum, "lr-tb", "lr", "lr-tb", "rl", "rl-tb", "tb", "tb-lr", "tb-rl"),
        Def("xml:base", true, PropertyDatatype.Uri, ""),
        Def("xml:lang", true, PropertyDatatype.String, ""),
        Def("z-index", false, PropertyDatatype.Number, "auto"),
    ];

    /// <summary>All property definitions, keyed by property name.</summary>
    public static IReadOnlyDictionary<string, PropertyDef> All { get; } =
        Definitions.ToDictionary(d => d.Name, StringComparer.Ordinal);

    /// <summary>The names of every property that inherits from its parent.</summary>
    public static IReadOnlySet<string> InheritedNames { get; } =
        Definitions.Where(d => d.Inherited).Select(d => d.Name).ToHashSet(StringComparer.Ordinal);

    /// <summary>Whether <paramref name="name"/> is a property the catalogue recognises.</summary>
    public static bool IsKnown(string name) => All.ContainsKey(BaseName(name));

    /// <summary>
    /// Whether <paramref name="name"/> inherits. A compound sub-property (e.g.
    /// <c>keep-together.within-page</c>) inherits with its base property. Unknown names are treated as
    /// not inherited.
    /// </summary>
    public static bool IsInherited(string name)
        => All.TryGetValue(BaseName(name), out PropertyDef? def) && def.Inherited;

    /// <summary>Looks up a property definition (by its base name), or <c>null</c> when unknown.</summary>
    public static PropertyDef? Lookup(string name) => All.GetValueOrDefault(BaseName(name));

    /// <summary>
    /// Strips a compound sub-property suffix (<c>.optimum</c>, <c>.within-page</c>, ...) so a compound
    /// component resolves to its base property's definition.
    /// </summary>
    internal static string BaseName(string name)
    {
        int dot = name.IndexOf('.');
        return dot < 0 ? name : name[..dot];
    }
}
