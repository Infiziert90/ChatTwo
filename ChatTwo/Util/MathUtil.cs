using System.Numerics;

namespace ChatTwo.Util;

public static class MathUtil
{
    public struct Rectangle
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;

        public static Rectangle FromPosAndSize(Vector2 pos, Vector2 size)
        {
            return new Rectangle
            {
                X = (int) pos.X,
                Y = (int) pos.Y,
                Width = (int) size.X,
                Height = (int) size.Y
            };
        }

        public int SizeX => X + Width;
        public int SizeY => Y + Height;
    }

    // From: https://stackoverflow.com/a/306379
    public static bool CheckRectOverlap(Rectangle a, Rectangle b)
    {
        bool ValueInRange(int value, int min, int max)
            => value > min && value < max;

        var xOverlap = ValueInRange(a.X, b.X, b.X + b.Width) || ValueInRange(b.X, a.X, a.X + a.Width);
        var yOverlap = ValueInRange(a.Y, b.Y, b.Y + b.Height) || ValueInRange(b.Y, a.Y, a.Y + a.Height);

        return xOverlap && yOverlap;
    }
}