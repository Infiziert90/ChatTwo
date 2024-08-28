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
}