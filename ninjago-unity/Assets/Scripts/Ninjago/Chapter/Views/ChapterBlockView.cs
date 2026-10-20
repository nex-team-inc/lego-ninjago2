#nullable enable

using System;

namespace Nex.Ninjago
{
    /// <summary>
    /// The view step of one block. It adds nothing on top of the stage's world and HUD frame, so replacing one block's
    /// view with the next only changes the (empty) overlay; the world and its camera keep running underneath.
    /// </summary>
    public class ChapterBlockView : SimpleCanvasView
    {
        string screenName = "chapter-block";

        public event Action? QuitRequested;

        #region View Implementation

        public override ViewIdentifier Identifier => ViewIdentifier.ChapterBlock;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;
        public override string AnalyticsScreenName => screenName;

        public override void OnBackButton()
        {
            if (!IsActive) return;
            NinjagoAnalytics.UiAction(AnalyticsScreenName, "back");
            QuitRequested?.Invoke();
        }

        #endregion

        #region Initialization

        public void Initialize(BlockType type)
        {
            screenName = $"chapter-block-{type.ToString().ToLowerInvariant()}";
        }

        #endregion
    }
}
