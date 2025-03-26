using System.Numerics;

namespace ChatTwo.Util;

public static class MathUtil
{
    public record Rectangle
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;

        public int SizeX;
        public int SizeY;

        public Rectangle(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;

            SizeX = X + Width;
            SizeY = Y + Height;
        }

        public Rectangle(Vector2 pos, Vector2 size)
            : this((int) pos.X, (int) pos.Y, (int) size.X, (int) size.Y) { }
    }

    // From: https://stackoverflow.com/a/306379
    /// <summary>
    /// Checks if two rectangles overlap at any point.
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns>True if overlapping</returns>
    public static bool HasOverlap(this Rectangle a, Rectangle b)
    {
        bool ValueInRange(int value, int min, int max)
            => value > min && value < max;

        var xOverlap = ValueInRange(a.X, b.X, b.X + b.Width) || ValueInRange(b.X, a.X, a.X + a.Width);
        var yOverlap = ValueInRange(a.Y, b.Y, b.Y + b.Height) || ValueInRange(b.Y, a.Y, a.Y + a.Height);

        return xOverlap && yOverlap;
    }
}