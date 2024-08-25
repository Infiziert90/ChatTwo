namespace ChatTwo.Http;

public static class WebserverUtil
{
    public static async Task<T> FrameworkWrapper<T>(Func<Task<T>> func)
    {
        return await Plugin.Framework.RunOnTick(func).ConfigureAwait(false);
    }

    // From: https://github.com/NancyFx/Nancy/blob/master/src/Nancy/Request.cs#L176
    /// <summary>
    /// Gets the cookie data from the provided string if it exists
    /// </summary>
    /// <param name="cookieHeader">The string containing cookie data</param>
    /// <returns>Cookies dictionary</returns>
    public static Dictionary<string, string> GetCookieData(string cookieHeader)
    {
        var cookieDictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (cookieHeader.Length == 0)
            return cookieDictionary;

        var values = cookieHeader.TrimEnd(';').Split(';');
        foreach (var parts in values.Select(c => c.Split(['='], 2)))
        {
            var cookieName = parts[0].Trim();
            var cookieValue = parts.Length == 1 ? string.Empty : parts[1]; //Cookie attribute

            cookieDictionary[cookieName] = cookieValue;
        }

        return cookieDictionary;
    }
}