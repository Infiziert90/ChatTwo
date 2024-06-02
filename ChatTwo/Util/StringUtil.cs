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

    // Taken from https://stackoverflow.com/a/4975942
    internal static string BytesToString(long byteCount) {
        string[] suf = ["B", "KB", "MB", "GB", "TB", "PB", "EB"]; // Longs run out around EB
        if (byteCount == 0)
            return "0" + suf[0];

        var bytes = Math.Abs(byteCount);
        var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
        var num = Math.Round(bytes / Math.Pow(1024, place), 1);
        return (Math.Sign(byteCount) * num).ToString("N0") + suf[place];
    }
}
