#nullable enable

namespace Nex.Ninjago
{
    /// <summary>
    /// The spiked walls of a Boss Clap (GDD §6.3) cover the top band of the sky grid: from BandBottom up to the grid top.
    /// The plane is hit when its top (y + half extent) is inside the band. Normal band 0.45 → bottom 3.36 (y 3.2 hit,
    /// y 2.8 safe); large band 0.7 → bottom 2.56 (safe on the bottom row).
    /// </summary>
    public readonly struct ClapWalls
    {
        public ClapWalls(ClapSize size, float gridBottom = 1.6f, float gridTop = 4.8f)
        {
            Size = size;
            GridBottom = gridBottom;
            GridTop = gridTop;
        }

        public ClapSize Size { get; }
        public float GridBottom { get; }
        public float GridTop { get; }
        public float BandBottom => GridTop - Size.band * (GridTop - GridBottom);

        public bool IsHit(float planeY, float halfExtentY) => planeY + halfExtentY > BandBottom;

        /// <summary>The highest plane centre that is still safe.</summary>
        public float SafeBelow(float halfExtentY) => BandBottom - halfExtentY;
    }
}
