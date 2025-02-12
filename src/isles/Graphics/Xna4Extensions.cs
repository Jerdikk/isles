// Copyright (c) Yufei Huang. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Xna.Framework.Graphics;

public static class Xna4Extensions
{
    private static readonly Stack<RenderTarget2D> _renderTargetStack = new();

    public static void PushRenderTarget(this GraphicsDevice graphicsDevice, RenderTarget2D renderTarget)
    {
        var renderTargets = graphicsDevice.GetRenderTargets();
        var current = renderTargets.Length > 0 ? (RenderTarget2D)renderTargets[0].RenderTarget : null;
        _renderTargetStack.Push(current);
        graphicsDevice.SetRenderTarget(renderTarget);
    }

    public static void PopRenderTarget(this GraphicsDevice graphicsDevice)
    {
        if (_renderTargetStack.Count > 0)
        {
            var renderTarget = _renderTargetStack.Pop();
            graphicsDevice.SetRenderTarget(renderTarget);
        }
    }

    public static void SetRenderState(
        this GraphicsDevice graphicsDevice,
        BlendState blendState = null,
        DepthStencilState depthStencilState = null,
        RasterizerState rasterizerState = null)
    {
        graphicsDevice.BlendState = blendState ?? BlendState.NonPremultiplied;
        graphicsDevice.DepthStencilState = depthStencilState ?? DepthStencilState.Default;
        graphicsDevice.RasterizerState = rasterizerState ?? RasterizerState.CullCounterClockwise;
    }
}
