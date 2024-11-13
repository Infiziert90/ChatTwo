using System.Runtime.CompilerServices;
using ChatTwo.Resources;
using Dalamud.Interface.ImGuiNotification;

namespace ChatTwo.Util;

public static class WrapperUtil
{
    public static void AddNotification(string content, NotificationType type, bool minimized = true)
    {
        Plugin.Notification.AddNotification(new Notification { Content = content, Type = type, Minimized = minimized });
    }

    public static void TryOpenURI(Uri uri)
    {
        try
        {
            Plugin.Log.Debug($"Opening URI {uri} in default browser");
            Dalamud.Utility.Util.OpenLink(uri.ToString());
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Error opening URI: {ex}");
            AddNotification(Language.Context_OpenInBrowserError, NotificationType.Error);
        }
    }

    public static IEnumerable<(T Value, int Index)> WithIndex<T>(this IEnumerable<T> list)
        => list.Select((x, i) => (x, i));

    /// <summary> Return the first object fulfilling the predicate or null for structs. </summary>
    /// <param name="values"> The enumerable. </param>
    /// <param name="predicate"> The predicate. </param>
    /// <returns> The first object fulfilling the predicate, or a null-optional. </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T? FirstOrNull<T>(this IEnumerable<T> values, Func<T, bool> predicate) where T : struct
    {
        foreach(var val in values)
            if (predicate(val))
                return val;

        return null;
    }
}