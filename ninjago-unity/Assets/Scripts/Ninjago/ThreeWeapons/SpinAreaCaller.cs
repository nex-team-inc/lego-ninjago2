#nullable enable

namespace Nex.Ninjago
{
    /// <summary>Calls the nunchuck areas: every four bursts cover all four areas in a shuffled order, never the same area twice in a row.</summary>
    public sealed class SpinAreaCaller
    {
        readonly SpinArea[] order = { SpinArea.LeftHead, SpinArea.RightHead, SpinArea.LeftBody, SpinArea.RightBody };
        readonly System.Random random;
        int next = int.MaxValue;
        bool started;

        public SpinArea Current { get; private set; }

        public SpinAreaCaller(int seed)
        {
            random = new System.Random(seed);
            Advance();
        }

        #region Public API

        public SpinArea Advance()
        {
            if (next >= order.Length)
            {
                Shuffle();
                // A new round must not open with the area that closed the last one.
                if (started && order[0] == Current) (order[0], order[order.Length - 1]) = (order[order.Length - 1], order[0]);
                next = 0;
            }

            started = true;
            Current = order[next++];
            return Current;
        }

        #endregion

        #region Helpers

        void Shuffle()
        {
            for (var i = order.Length - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
        }

        #endregion
    }
}
