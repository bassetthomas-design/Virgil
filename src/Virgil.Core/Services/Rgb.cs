namespace Virgil.Core.Services
{
    /// <summary>
    /// Lightweight RGB structure used by services to express colours without
    /// depending on WPF or other UI frameworks.
    /// </summary>
    public readonly struct Rgb
    {
        public readonly byte R;
        public readonly byte G;
        public readonly byte B;
        public Rgb(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }
    }
}
