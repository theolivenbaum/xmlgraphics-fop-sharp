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

using Fop.Colors;
using Fop.Traits;

namespace Fop.Fo;

/// <summary>
/// The resolved border for a single physical edge of a box: its width, colour and style. A border
/// edge only paints when <see cref="IsVisible"/> is true (width &gt; 0 and style is neither
/// <see cref="BorderStyle.None"/> nor <see cref="BorderStyle.Hidden"/>).
/// </summary>
/// <param name="Width">The border width.</param>
/// <param name="Color">The border colour.</param>
/// <param name="Style">The border style.</param>
public readonly record struct BorderEdge(FoLength Width, FopColor Color, BorderStyle Style)
{
    /// <summary>Whether this edge should be painted.</summary>
    public bool IsVisible => Width.Millipoints > 0 && Style is not (BorderStyle.None or BorderStyle.Hidden);
}

/// <summary>The <c>background-repeat</c> property values: how a background image tiles within the box.</summary>
public enum BackgroundRepeat
{
    /// <summary>Tile in both directions (the <c>repeat</c> default).</summary>
    Repeat,

    /// <summary>Tile horizontally only (<c>repeat-x</c>).</summary>
    RepeatX,

    /// <summary>Tile vertically only (<c>repeat-y</c>).</summary>
    RepeatY,

    /// <summary>Place a single copy (<c>no-repeat</c>).</summary>
    NoRepeat,
}

/// <summary>
/// A resolved <c>background-position</c> component (horizontal or vertical). It is either an absolute
/// length offset (<see cref="IsPercent"/> false) or a percentage (<see cref="IsPercent"/> true), the
/// latter resolved at paint time against <c>(padding-box dimension − image dimension)</c> -- the CSS /
/// FOP semantics (see <c>TraitSetter.addBackground</c>). The offset is applied only along an axis in
/// which the image does not tile (matching FOP).
/// </summary>
/// <param name="LengthMpt">The absolute offset in millipoints when <see cref="IsPercent"/> is false.</param>
/// <param name="Percent">The fraction (0..1) when <see cref="IsPercent"/> is true.</param>
/// <param name="IsPercent">Whether this is a percentage rather than an absolute length.</param>
public readonly record struct BackgroundPosition(double LengthMpt, double Percent, bool IsPercent)
{
    /// <summary>A zero offset (the <c>0pt</c>/<c>left</c>/<c>top</c> default).</summary>
    public static readonly BackgroundPosition Zero = default;

    /// <summary>An absolute length offset, in millipoints.</summary>
    public static BackgroundPosition FromLength(double millipoints) => new(millipoints, 0, false);

    /// <summary>A percentage offset (the fraction resolved against the free space at paint time).</summary>
    public static BackgroundPosition FromPercent(double fraction) => new(0, fraction, true);

    /// <summary>Resolves the offset in millipoints given the free space (padding-box − image) along the axis.</summary>
    public double Resolve(double freeSpaceMpt) => IsPercent ? Percent * freeSpaceMpt : LengthMpt;
}

/// <summary>
/// A resolved <c>background-image</c> reference together with its tiling and positioning. Mirrors the
/// image-related fields of FOP's <c>CommonBorderPaddingBackground</c>. The image is painted within the
/// <em>padding box</em> (the border box minus border widths), clipped to it, and tiled according to
/// <see cref="Repeat"/>; <see cref="PositionHorizontal"/>/<see cref="PositionVertical"/> offset a
/// non-tiling axis.
/// </summary>
/// <param name="Uri">The resolved image URI/path (the <c>url(...)</c> target or a bare reference).</param>
/// <param name="Repeat">How the image tiles.</param>
/// <param name="PositionHorizontal">The horizontal position (applied when not tiling horizontally).</param>
/// <param name="PositionVertical">The vertical position (applied when not tiling vertically).</param>
public sealed record BackgroundImage(
    string Uri,
    BackgroundRepeat Repeat,
    BackgroundPosition PositionHorizontal,
    BackgroundPosition PositionVertical);

/// <summary>
/// The resolved box-model properties of a formatting object: per-edge borders and padding (in
/// physical top/right/bottom/left terms) plus an optional background colour and background image.
/// Border, padding and background are <em>not</em> inherited.
/// <para>
/// Writing-mode-relative edges (before/after/start/end) are mapped to physical edges assuming the
/// default lr-tb writing mode: before=top, after=bottom, start=left, end=right.
/// </para>
/// </summary>
/// <param name="BorderTop">The top border edge.</param>
/// <param name="BorderRight">The right border edge.</param>
/// <param name="BorderBottom">The bottom border edge.</param>
/// <param name="BorderLeft">The left border edge.</param>
/// <param name="PaddingTop">Top padding.</param>
/// <param name="PaddingRight">Right padding.</param>
/// <param name="PaddingBottom">Bottom padding.</param>
/// <param name="PaddingLeft">Left padding.</param>
/// <param name="BackgroundColor">The background colour, or <c>null</c> when transparent/unset.</param>
/// <param name="BackgroundImage">The background image, or <c>null</c> when none is set.</param>
public readonly record struct BoxProperties(
    BorderEdge BorderTop,
    BorderEdge BorderRight,
    BorderEdge BorderBottom,
    BorderEdge BorderLeft,
    FoLength PaddingTop,
    FoLength PaddingRight,
    FoLength PaddingBottom,
    FoLength PaddingLeft,
    FopColor? BackgroundColor,
    BackgroundImage? BackgroundImage = null)
{
    /// <summary>Whether any border edge is visible.</summary>
    public bool HasBorder =>
        BorderTop.IsVisible || BorderRight.IsVisible || BorderBottom.IsVisible || BorderLeft.IsVisible;

    /// <summary>Whether a background colour is set.</summary>
    public bool HasBackgroundColor => BackgroundColor is not null;

    /// <summary>Whether a background image is set.</summary>
    public bool HasBackgroundImage => BackgroundImage is not null;

    /// <summary>Whether any background (colour or image) is set.</summary>
    public bool HasBackground => HasBackgroundColor || HasBackgroundImage;

    /// <summary>Whether the box draws anything (border or background).</summary>
    public bool IsEmpty => !HasBorder && !HasBackground;

    /// <summary>The total inset on the left edge (left border width + left padding), in millipoints.</summary>
    public double LeftInsetMpt => BorderLeft.Width.Millipoints + PaddingLeft.Millipoints;

    /// <summary>The total inset on the right edge (right border width + right padding), in millipoints.</summary>
    public double RightInsetMpt => BorderRight.Width.Millipoints + PaddingRight.Millipoints;

    /// <summary>The total inset on the top edge (top border width + top padding), in millipoints.</summary>
    public double TopInsetMpt => BorderTop.Width.Millipoints + PaddingTop.Millipoints;

    /// <summary>The total inset on the bottom edge (bottom border width + bottom padding), in millipoints.</summary>
    public double BottomInsetMpt => BorderBottom.Width.Millipoints + PaddingBottom.Millipoints;
}
