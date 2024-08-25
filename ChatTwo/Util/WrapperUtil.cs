using Dalamud.Interface.ImGuiNotification;

namespace ChatTwo.Util;

public static class WrapperUtil
{
    public static void AddNotification(string content, NotificationType type, bool minimized = true)
    {
        Plugin.Notification.AddNotification(new Notification { Content = content, Type = type, Minimized = minimized });
    }

    public static IEnumerable<(T Value, int Index)> WithIndex<T>(this IEnumerable<T> list)
        => list.Select((x, i) => (x, i));
}