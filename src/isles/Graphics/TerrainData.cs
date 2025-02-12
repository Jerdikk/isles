// Copyright (c) Yufei Huang. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Isles.Graphics;

public class TerrainData
{
    public float Step { get; init; }
    public float MinHeight { get; init; }
    public float MaxHeight { get; init; }
    public string Heightmap { get; init; }
    public string WaterTexture { get; init; }
    public string WaterBumpTexture { get; init; }
    public Layer[] Layers { get; init; }

    public struct Layer
    {
        public string ColorTexture { get; init; }
        public string AlphaTexture { get; init; }
    }
}
