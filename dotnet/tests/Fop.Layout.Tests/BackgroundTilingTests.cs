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
/// Tests for <see cref="BackgroundTiling.Compute"/> -- the tile-count and offset arithmetic shared by
/// the PDF renderers, mirroring FOP's <c>AbstractPathOrientedRenderer.drawBackground</c>.
/// </summary>
public sealed class BackgroundTilingTests
{
    // A 25x10 padding rect with a 10x10 tile: FOP adds one extra tile per repeating axis.
    private const double Clip = 25_000;
    private const double ClipH = 30_000;
    private const double Tile = 10_000;

    [Fact]
    public void Repeat_TilesBothAxesWithCoverageOvershoot()
    {
        BackgroundTiling t = BackgroundTiling.Compute(
            Clip, ClipH, Tile, Tile, BackgroundRepeat.Repeat,
            BackgroundPosition.Zero, BackgroundPosition.Zero);

        Assert.Equal(3, t.HorizontalCount); // (int)(25000/10000 + 1) = 3
        Assert.Equal(4, t.VerticalCount);   // (int)(30000/10000 + 1) = 4
        Assert.Equal(0, t.OffsetXMpt, 3);   // no offset on a repeating axis
        Assert.Equal(0, t.OffsetYMpt, 3);
    }

    [Fact]
    public void NoRepeat_SingleTileBothAxes()
    {
        BackgroundTiling t = BackgroundTiling.Compute(
            Clip, ClipH, Tile, Tile, BackgroundRepeat.NoRepeat,
            BackgroundPosition.Zero, BackgroundPosition.Zero);

        Assert.Equal(1, t.HorizontalCount);
        Assert.Equal(1, t.VerticalCount);
    }

    [Fact]
    public void RepeatX_TilesHorizontallyOnly()
    {
        BackgroundTiling t = BackgroundTiling.Compute(
            Clip, ClipH, Tile, Tile, BackgroundRepeat.RepeatX,
            BackgroundPosition.Zero, BackgroundPosition.Zero);

        Assert.Equal(3, t.HorizontalCount);
        Assert.Equal(1, t.VerticalCount);
    }

    [Fact]
    public void RepeatY_TilesVerticallyOnly()
    {
        BackgroundTiling t = BackgroundTiling.Compute(
            Clip, ClipH, Tile, Tile, BackgroundRepeat.RepeatY,
            BackgroundPosition.Zero, BackgroundPosition.Zero);

        Assert.Equal(1, t.HorizontalCount);
        Assert.Equal(4, t.VerticalCount);
    }

    [Fact]
    public void Position_AppliedOnlyToNonRepeatingAxis()
    {
        // no-repeat both axes: position resolves against free space (clip - tile).
        // 50% horizontal => 0.5 * (25000 - 10000) = 7500. 100% vertical => 1.0 * (30000 - 10000) = 20000.
        BackgroundTiling t = BackgroundTiling.Compute(
            Clip, ClipH, Tile, Tile, BackgroundRepeat.NoRepeat,
            BackgroundPosition.FromPercent(0.5), BackgroundPosition.FromPercent(1.0));

        Assert.Equal(7_500, t.OffsetXMpt, 3);
        Assert.Equal(20_000, t.OffsetYMpt, 3);
    }

    [Fact]
    public void Position_IgnoredOnRepeatingAxis()
    {
        // repeat-x: the horizontal axis tiles, so the horizontal position offset is dropped.
        BackgroundTiling t = BackgroundTiling.Compute(
            Clip, ClipH, Tile, Tile, BackgroundRepeat.RepeatX,
            BackgroundPosition.FromPercent(0.5), BackgroundPosition.FromLength(3_000));

        Assert.Equal(0, t.OffsetXMpt, 3);    // horizontal repeats -> no offset
        Assert.Equal(3_000, t.OffsetYMpt, 3); // vertical single -> absolute offset honoured
    }

    [Fact]
    public void Position_AbsoluteLength()
    {
        BackgroundTiling t = BackgroundTiling.Compute(
            Clip, ClipH, Tile, Tile, BackgroundRepeat.NoRepeat,
            BackgroundPosition.FromLength(4_000), BackgroundPosition.FromLength(-2_000));

        Assert.Equal(4_000, t.OffsetXMpt, 3);
        Assert.Equal(-2_000, t.OffsetYMpt, 3);
    }
}
