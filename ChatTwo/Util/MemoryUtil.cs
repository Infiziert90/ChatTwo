using System.Text;

namespace ChatTwo.Util;

public static class MemoryUtil
{
    public static unsafe void PrintMemoryArea(nint address, int length)
    {
        var ptr = (byte*)address;
        var str = new StringBuilder("\n");
        for(var i = 0; i < length; i++)
        {
            str.Append($"{ptr![i]:X02}");

            if (i == 0)
                continue;

            if ((i+1) % 16 == 0)
                str.Append('\n');
            else if ((i+1) % 4 == 0)
                str.Append(' ');
        }

        Plugin.Log.Information(str.ToString());
    }
}