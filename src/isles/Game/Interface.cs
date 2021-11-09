// Copyright (c) Yufei Huang. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Isles;

public interface IScreen : IEventListener
{
    void Update(GameTime gameTime);
    void Draw(GameTime gameTime);
}

/// <summary>
/// Interface for a landscape.
/// The landscape lays on the XY plane, Z value is used to represent the height.
/// The position of the landscape is fixed at (0, 0).
/// </summary>
public interface ILandscape
{
    /// <summary>
    /// Gets the size of the landscape.
    /// </summary>
    Vector3 Size { get; }

    /// <summary>
    /// Gets the height (Z value) of a point (x, y) on the landscape.
    /// </summary>
    float GetHeight(float x, float y);

    /// <summary>
    /// Gets the number of grids.
    /// </summary>
    Point GridCount { get; }

    /// <summary>
    /// Gets whether the point is walkable (E.g., above water).
    /// </summary>
    bool IsPointOccluded(float x, float y);
}
