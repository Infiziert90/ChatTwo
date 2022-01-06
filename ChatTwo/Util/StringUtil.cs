using System.Text;

namespace ChatTwo.Util;

internal static class StringUtil {
    internal static byte[] ToTerminatedBytes(this string s) {
        var unterminated = Encoding.UTF8.GetBytes(s);
        var bytes = new byte[unterminated.Length + 1];
        Array.Copy(unterminated, bytes, unterminated.Length);
        bytes[^1] = 0;
        return bytes;
    }
}
