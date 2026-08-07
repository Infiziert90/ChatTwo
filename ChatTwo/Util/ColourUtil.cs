using System.Buffers.Binary;
using System.Numerics;

namespace ChatTwo.Util;

public static class ColourUtil
{
    /// <summary>
    /// Converts an RGB/A color value to a Vector3.
    /// </summary>
    /// <param name="rgba">The color</param>
    /// <returns>Vector3 with RGB representation</returns>
    /// <remarks>RGBA input will drop alpha</remarks>
    public static Vector3 RgbaToVector3(uint rgba)
    {
        var (r, g, b, _) = RgbaToRgbaComponents(rgba);
        return new Vector3((float) r / 255, (float) g / 255, (float) b / 255);
    }

    /// <summary>
    /// Converts an RGBA color value to a Vector4.
    /// </summary>
    /// <param name="rgba">The color</param>
    /// <returns>Vector4 with RGBA representation</returns>
    public static Vector4? RgbaToVector4(uint? rgba)
    {
        if (rgba == null)
            return null;

        var (r, g, b, a) = RgbaToRgbaComponents(rgba.Value);
        return new Vector4((float)r / 255, (float)g / 255, (float)b / 255, (float)a / 255);
    }

    /// <summary>
    /// Converts a Vector3 to an RGBA color value.
    /// </summary>
    /// <param name="col">The color</param>
    /// <returns>Color as byte representation RR GG BB AA</returns>
    public static uint Vector3ToRgba(Vector3 col)
        => ComponentsToRgba((byte)Math.Round(col.X * 255), (byte)Math.Round(col.Y * 255), (byte)Math.Round(col.Z * 255));

    /// <summary>
    /// Converts a Vector4 to an RGBA color value.
    /// </summary>
    /// <param name="col">The color</param>
    /// <returns>Color as byte representation RR GG BB AA</returns>
    public static uint Vector4ToRgba(Vector4 col)
        => ComponentsToRgba((byte)Math.Round(col.X * 255), (byte)Math.Round(col.Y * 255), (byte)Math.Round(col.Z * 255), (byte)Math.Round(col.W * 255));

    /// <summary>
    /// Converts a Vector4 to an ABGR color value.
    /// </summary>
    /// <param name="col">The color</param>
    /// <returns>Color as byte presentation AA BB GG RR</returns>
    /// <remarks>ImGui uses ABGR for colors pushed as integer</remarks>
    public static uint Vector4ToAbgr(Vector4 col)
        => RgbaToAbgr(ComponentsToRgba((byte)Math.Round(col.X * 255), (byte)Math.Round(col.Y * 255), (byte)Math.Round(col.Z * 255), (byte)Math.Round(col.W * 255)));

    /// <summary>
    /// Converts an ARGB color value to RGBA.
    /// </summary>
    /// <param name="col">The color</param>
    /// <returns>Color as byte representation RR GG BB AA</returns>
    /// <remarks>The game returns all GlobalValue colors as ARGB</remarks>
    public static unsafe uint ArgbToRgba(uint col)
    {
        // Skip color with no value
        if (col == 0)
            return 0;

        // Check if Alpha is set, if not set to 255
        if (col <= 0x00FFFFFFu)
            col |= 0xFF000000u;

        var buf = (byte*)&col;
        (buf[1], buf[2], buf[3], buf[0]) = (buf[0], buf[1], buf[2], buf[3]);
        return col;
    }

    /// <summary>
    /// Convert an RGBA color value to ABGR
    /// </summary>
    /// <param name="rgba">The color</param>
    /// <returns>Color as byte representation AA BB GG RR</returns>
    public static uint RgbaToAbgr(uint rgba)
        => BinaryPrimitives.ReverseEndianness(rgba);

    /// <summary>
    /// Converts the given components to an RGBA color value as uint.
    /// </summary>
    /// <param name="red">The byte value for red</param>
    /// <param name="green">The byte value for green</param>
    /// <param name="blue">The byte value for blue</param>
    /// <param name="alpha">The byte value for alpha</param>
    /// <returns>Color as byte representation RR GG BB AA</returns>
    public static uint ComponentsToRgba(byte red, byte green, byte blue, byte alpha = 0xFF)
        => alpha | (uint) (red << 24) | (uint) (green << 16) | (uint) (blue << 8);

    /// <summary>
    /// Converts an RGBA color value into its components.
    /// </summary>
    /// <param name="rgba">The color</param>
    /// <returns>The color components</returns>
    private static (byte r, byte g, byte b, byte a) RgbaToRgbaComponents(uint rgba)
    {
        var r = (byte)((rgba & 0xFF000000) >> 24);
        var g = (byte)((rgba & 0xFF0000) >> 16);
        var b = (byte)((rgba & 0xFF00) >> 8);
        var a = (byte)(rgba & 0xFF);
        return (r, g, b, a);
    }
}
