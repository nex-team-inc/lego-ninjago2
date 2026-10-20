#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// One player's side of a fight: the ninja, the enemy whose threats are marked for this player, the thrown rock and
    /// its stones, the window markers and that player's readers. The lane's LocalClock slows only this ninja and this
    /// player's thrown thing during a window, so two players' windows never fight over the world's time.
    /// </summary>
    public sealed class ChapterFightLane
    {
        public PlayerBody Body { get; }
        public ChapterNinja Ninja { get; }
        public ChapterEnemy Attacker { get; set; }
        public Transform Rock { get; }
        public RockPieces Pieces { get; }
        public WindowMarker Marker { get; }
        public WindowMarker Arrow { get; }
        public Transform HangPoint { get; }
        public LocalClock Clock { get; } = new();
        public ShoulderSlash Slash { get; } = new();
        public SlipDetector Slip { get; } = new();
        public WiggleMeter Wiggle { get; } = new();
        public PlayerKicks Kicks { get; }
        public SweepSide NextSafeSide { get; set; }
        /// <summary>A monster sits on this ninja until the player wiggles free.</summary>
        public bool Pinned { get; set; }
        /// <summary>Another lane is pinned: this lane's enemies freeze.</summary>
        public bool Frozen { get; set; }
        public bool KickOpen { get; set; }
        public int QueuedKicks { get; set; }

        public int PlayerIndex => Body.PlayerIndex;
        public Color Color => Body.Color;
        /// <summary>Real seconds this frame for windows; 0 while frozen.</summary>
        public float RealDelta => Frozen ? 0f : Time.unscaledDeltaTime;
        /// <summary>Lane-time seconds this frame for enemy motion; 0 while frozen.</summary>
        public float LocalDelta => Frozen ? 0f : Clock.DeltaTime;

        public ChapterFightLane(PlayerBody body, ChapterNinja ninja, ChapterEnemy attacker, Transform rock, RockPieces pieces, WindowMarker marker,
            WindowMarker arrow, Transform hangPoint, SweepSide firstSafeSide)
        {
            Body = body;
            Ninja = ninja;
            Attacker = attacker;
            Rock = rock;
            Pieces = pieces;
            Marker = marker;
            Arrow = arrow;
            HangPoint = hangPoint;
            Kicks = new PlayerKicks(body);
            NextSafeSide = firstSafeSide;
        }

        #region Lane Time

        /// <summary>Waits in this lane's time, so a frozen or slowed lane waits longer.</summary>
        public async UniTask WaitAsync(float seconds, CancellationToken cancellationToken)
        {
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += LocalDelta;
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        public async UniTask AnimateAsync(float seconds, Action<float> step, CancellationToken cancellationToken)
        {
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += LocalDelta;
                step(Mathf.Clamp01(elapsed / seconds));
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            step(1f);
        }

        #endregion
    }
}
