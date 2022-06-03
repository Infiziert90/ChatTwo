using System.Text;

namespace ChatTwo.Util;

internal static class StringUtil {
    internal static byte[] ToTerminatedBytes(this string s) {
        var utf8 = Encoding.UTF8;
        var bytes = new byte[utf8.GetByteCount(s) + 1];
        utf8.GetBytes(s, 0, s.Length, bytes, 0);
        bytes[^1] = 0;
        return bytes;
    }
}
