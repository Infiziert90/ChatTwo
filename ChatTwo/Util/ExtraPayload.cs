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
        if (typeByte == 0xEC)
        {
            payload.Enabled = false;
            return payload;
        }

        if (typeByte == 0xE9)
        {
            var param = stream.ReadByte();
            var ok = TryGetGNumDefault((uint) (param - 2), out var value);
            if (!ok)
            {
                Plugin.Log.Error($"Unable to GetGNum for param {param - 2}");
                return null;
            }

            payload.Enabled = true;
            payload.UnshiftedColor = value;
            payload.Color = ColourUtil.ArgbToRgba(value);

            return payload;
        }

        if (typeByte is >= 0xF0 and <= 0xFE)
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

        return null;
    }

    private static unsafe bool TryGetGNumDefault(uint parameterIndex, out uint value)
    {
        value = 0u;

        var rtm = RaptureTextModule.Instance();
        if (rtm is null)
            return false;

        if (!ThreadSafety.IsMainThread)
        {
            Plugin.Log.Error("Global parameters may only be used from the main thread.");
            return false;
        }

        ref var gp = ref rtm->TextModule.MacroDecoder.GlobalParameters;
        if (parameterIndex >= gp.MySize)
            return false;

        var p = rtm->TextModule.MacroDecoder.GlobalParameters.Get(parameterIndex);
        switch (p.Type)
        {
            case TextParameterType.Integer:
                value = (uint)p.IntValue;
                return true;
            default:
                return false;
        }
    }
}