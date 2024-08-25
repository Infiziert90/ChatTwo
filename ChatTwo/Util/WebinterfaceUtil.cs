namespace ChatTwo.Util;

public class WebinterfaceUtil
{
    private static readonly Random Rng = new();

    public static string GenerateSimpleAuthCode()
    {
        return (100000 + Rng.Next() % 100000).ToString()[1..];
    }

    public static string GenerateSimpleToken()
    {
        var buffer = new byte[15];
        Rng.NextBytes(buffer);

        return Convert.ToHexString(buffer);
    }
}