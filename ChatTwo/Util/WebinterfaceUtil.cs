namespace ChatTwo.Util;

public class WebinterfaceUtil
{
    private static readonly Random Rng = new();

    public static string GenerateSimpleAuthCode()
    {
        return (100000 + Rng.Next() % 100000).ToString()[1..];
    }
}