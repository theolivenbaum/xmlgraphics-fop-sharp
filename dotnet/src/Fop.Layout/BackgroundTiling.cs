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

namespace Fop.Layout;

/// <summary>
/// The tiling plan for a background image: how many copies to lay across the padding rectangle in each
/// direction and the offset of the first tile from the padding origin (all in millipoints). Computed
/// the same way as FOP's <c>AbstractPathOrientedRenderer.drawBackground</c>: one extra tile is added
/// in a repeating direction so the rectangle is fully covered (the renderer clips the overflow), and
/// the background-position offset is applied only along a non-repeating axis.
/// </summary>
/// <param name="HorizontalCount">Number of tiles across (≥ 1).</param>
/// <param name="VerticalCount">Number of tiles down (≥ 1).</param>
/// <param name="OffsetXMpt">Offset of the first tile from the padding left edge, in millipoints.</param>
/// <param name="OffsetYMpt">Offset of the first tile from the padding top edge, in millipoints.</param>
public readonly record struct BackgroundTiling(
    int HorizontalCount, int VerticalCount, double OffsetXMpt, double OffsetYMpt)
{
    /// <summary>
    /// Computes the tiling plan for an image of intrinsic size (<paramref name="tileWidthMpt"/>,
    /// <paramref name="tileHeightMpt"/>) filling a padding rectangle of (<paramref name="clipWidthMpt"/>,
    /// <paramref name="clipHeightMpt"/>) under <paramref name="repeat"/>. The position offsets resolve a
    /// percentage against the free space <c>(clip − tile)</c> along their axis, and apply only where that
    /// axis does not tile.
    /// </summary>
    public static BackgroundTiling Compute(
        double clipWidthMpt, double clipHeightMpt, double tileWidthMpt, double tileHeightMpt,
        BackgroundRepeat repeat, BackgroundPosition positionHorizontal, BackgroundPosition positionVertical)
    {
        // FOP: horzCount = (int)((paddRectWidth / targetWidth) + 1). One extra tile guarantees coverage;
        // the renderer clips the overflow.
        int horizontalCount = (int)(clipWidthMpt / tileWidthMpt + 1.0);
        int verticalCount = (int)(clipHeightMpt / tileHeightMpt + 1.0);

        switch (repeat)
        {
            case BackgroundRepeat.NoRepeat:
                horizontalCount = 1;
                verticalCount = 1;
                break;
            case BackgroundRepeat.RepeatX:
                verticalCount = 1;
                break;
            case BackgroundRepeat.RepeatY:
                horizontalCount = 1;
                break;
        }

        horizontalCount = Math.Max(1, horizontalCount);
        verticalCount = Math.Max(1, verticalCount);

        double offsetX = horizontalCount == 1 ? positionHorizontal.Resolve(clipWidthMpt - tileWidthMpt) : 0;
        double offsetY = verticalCount == 1 ? positionVertical.Resolve(clipHeightMpt - tileHeightMpt) : 0;

        return new BackgroundTiling(horizontalCount, verticalCount, offsetX, offsetY);
    }
}
