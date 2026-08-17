using Lumina.Text.Payloads;

namespace ChatTwo.Util;

public class ColorPayload
{
    private const byte StartByte = 2;

    public bool Enabled;
    public uint Color;
    public uint UnshiftedColor;
    public MacroCode MacroCode;

    public static ColorPayload? From(byte[] data, MacroCode macroCode = MacroCode.Color)
    {
        using var stream = new MemoryStream(data);
        if (stream.ReadByte() != StartByte || stream.ReadByte() != (byte) macroCode)
            return null;

        stream.ReadByte(); // skip the length byte;

        var typeByte = stream.ReadByte();
        var payload = new ColorPayload { MacroCode = macroCode };
        switch (typeByte)
        {
            case 0xEC:
                payload.Enabled = false;
                return payload;
            case 0xE9:
                var param = stream.ReadByte();
                var globalValue = (uint) GlobalParametersCache.GetValue(param - 2);
                payload.Enabled = true;
                payload.UnshiftedColor = globalValue;
                payload.Color = ColourUtil.ArgbToRgba(globalValue);

                return payload;
            case >= 0xF0 and <= 0xFE:
                // From: https://github.com/NotAdam/Lumina/blob/master/src/Lumina/Text/Expressions/IntegerExpression.cs#L119-L128
                // Component bytes are never zero in this encoding, so zero doubles
                // as the truncation sentinel (EOF or premature null); payload data
                // comes from the game or other plugins, so malformed input must
                // return null rather than throw.
                var truncated = false;
                uint ReadComponent(int shift)
                {
                    var v = stream.ReadByte();
                    if (v <= 0)
                    {
                        truncated = true;
                        return 0;
                    }

                    return (uint) v << shift;
                }

                typeByte += 1;
                var argbValue = 0u;
                if ((typeByte & 8) != 0)
                    argbValue |= ReadComponent(24);
                else
                    argbValue |= 0xFF000000u;

                if ((typeByte & 4) != 0) argbValue |= ReadComponent(16);
                if ((typeByte & 2) != 0) argbValue |= ReadComponent(8);
                if ((typeByte & 1) != 0) argbValue |= ReadComponent(0);

                if (truncated)
                    return null;

                payload.Enabled = true;
                payload.Color = ColourUtil.ArgbToRgba(argbValue);

                return payload;
            case >= 0x01 and <= 0xCF:
                // Inline small integer: a single byte encoding value + 1. The game pushes the
                // resolved value as-is; 0 draws no edge (verified against the vanilla renderer).
                payload.Enabled = true;
                payload.Color = ColourUtil.ArgbToRgba((uint) (typeByte - 1));

                return payload;
            default:
                return null;
        }
    }
}