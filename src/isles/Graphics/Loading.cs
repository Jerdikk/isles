// Copyright (c) Yufei Huang. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Isles.Graphics;

/// <summary>
/// Interface for tracking loading progress.
/// </summary>
public interface ILoading
{
    void Refresh(float newProgress);
}

/// <summary>
/// Used to presents loading progress.
/// </summary>
public class Loading : ILoading
{
    /// <summary>
    /// Game graphics.
    /// </summary>
    private readonly GraphicsDevice graphics;
    private readonly ProgressBar progressBar;
    private readonly UIDisplay uiDisplay;

    private DateTime _lastRefreshTime = DateTime.UtcNow;

    /// <summary>
    /// Gets current progress.
    /// </summary>
    public float Progress { get; private set; }

    /// <summary>
    /// Refresh the loading screen with the new progress.
    /// </summary>
    /// <param name="newProgress"></param>
    /// <param name="newMessage"></param>
    public void Refresh(float newProgress)
    {
        Progress = newProgress;
        progressBar.SetProgress((int)newProgress);
        if (Progress < 0)
        {
            Progress = 0;
        }
        else if (Progress > 100)
        {
            Progress = 100;
        }

        var now = DateTime.UtcNow;
        if (now - _lastRefreshTime > TimeSpan.FromSeconds(1))
        {
            Draw();
            _lastRefreshTime = now;
        }
    }

    /// <summary>
    /// Create a loading screen for a given graphics device.
    /// </summary>
    /// <param name="graphics"></param>
    public Loading(GraphicsDevice graphics)
    {
        this.graphics = graphics;
        uiDisplay = new UIDisplay(BaseGame.Singleton);
        progressBar = new ProgressBar
        {
            Texture = BaseGame.Singleton.TextureLoader.LoadTexture("data/ui/ProgressBar.png"),
            SourceRectangleLeftEnd = new Rectangle(0, 0, 64, 63),
            SourceRectangleHightLight = new Rectangle(0, 126, 85, 63),
            SourceRectangleRightEnd = new Rectangle(165, 0, 64, 63),
            SourceRectangleFiller = new Rectangle(80, 0, 40, 63),
            StartingTime = BaseGame.Singleton.CurrentGameTime.TotalGameTime.TotalSeconds,
            HighLightRollingSpeed = 180,
            HighLightLength = 50,
            Area = new Rectangle(181, 269, 435, 6),// new Rectangle(181, 323, 435, 7);
            Anchor = Anchor.TopLeft,
            ScaleMode = ScaleMode.Stretch,
        };

        var height = 875.0 * uiDisplay.Area.Width / 1400;

        var backgroundPanel = new Panel(new Rectangle(0, (int)((uiDisplay.Area.Height - height) / 2),
                                uiDisplay.Area.Width, (int)height))
        {
            Texture = BaseGame.Singleton.TextureLoader.LoadTexture("data/ui/LoadingDisplay.png"),
        };
        backgroundPanel.SourceRectangle = new Rectangle(0, 0, backgroundPanel.Texture.Width, backgroundPanel.Texture.Height);
        backgroundPanel.Anchor = Anchor.Center;
        backgroundPanel.ScaleMode = ScaleMode.Stretch;

        var panel = new Panel(new Rectangle(0, (int)((uiDisplay.Area.Height - height) / 2),
                                uiDisplay.Area.Width, (int)height))
        {
            Texture = BaseGame.Singleton.TextureLoader.LoadTexture("data/ui/LoadingDisplay.png"),
        };
        panel.SourceRectangle = new Rectangle(0, 0, panel.Texture.Width, panel.Texture.Height);
        panel.Anchor = Anchor.Center;
        panel.ScaleMode = ScaleMode.Stretch;

        uiDisplay.Add(backgroundPanel);
        backgroundPanel.Add(progressBar);
        uiDisplay.Add(panel);
        uiDisplay.Draw(BaseGame.Singleton.CurrentGameTime);
    }

    private void Draw()
    {
        graphics.Clear(Color.Black);
        uiDisplay.Draw(BaseGame.Singleton.CurrentGameTime);
        graphics.Present();
    }
}
