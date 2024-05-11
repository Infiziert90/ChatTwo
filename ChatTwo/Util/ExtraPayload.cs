using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.Text;

namespace ChatTwo.Util;

public class ColorPayload
{
    private const byte START_BYTE = 2;

    public bool Enabled;
    public uint Color;
    public uint UnshiftedColor;

    public static ColorPayload? From(byte[] data)
    {
        using var stream = new MemoryStream(data);
        if (stream.ReadByte() != START_BYTE || stream.ReadByte() != 0x13)
            return null;

        stream.ReadByte(); // skip the length byte;

        var typeByte = stream.ReadByte();
        var payload = new ColorPayload();
        switch (typeByte)
        {
            case 0xEC:
                payload.Enabled = false;
                return payload;
            case 0xE9:
            {
                var param = stream.ReadByte();
                var value = (uint) GlobalParametersCache.GetValue(param - 2);
                payload.Enabled = true;
                payload.UnshiftedColor = value;
                payload.Color = ColourUtil.ArgbToRgba(value);

                return payload;
            }
            case >= 0xF0 and <= 0xFE:
            {
                // From: https://github.com/NotAdam/Lumina/blob/master/src/Lumina/Text/Expressions/IntegerExpression.cs#L119-L128
                uint ShiftAndThrowIfZero(int v, int shift)
                {
                    return v switch
                    {
                        -1 => throw new ArgumentException("Encountered premature end of input (unexpected EOF).", nameof(v)),
                        0 => throw new ArgumentException("Encountered premature end of input (unexpected null character).", nameof(v)),
                        _ => (uint)v << shift
                    };
                }

                typeByte += 1;
                var value = 0u;
                if ((typeByte & 8) != 0)
                    value |= ShiftAndThrowIfZero(stream.ReadByte(), 24);
                else
                    value |= 0xff000000u;

                if( (typeByte & 4) != 0 ) value |= ShiftAndThrowIfZero( stream.ReadByte(), 16 );
                if( (typeByte & 2) != 0 ) value |= ShiftAndThrowIfZero( stream.ReadByte(), 8 );
                if( (typeByte & 1) != 0 ) value |= ShiftAndThrowIfZero( stream.ReadByte(), 0 );

                payload.Enabled = true;
                payload.Color = ColourUtil.ArgbToRgba(value);

                return payload;
            }
            default:
                return null;
        }
    }
}