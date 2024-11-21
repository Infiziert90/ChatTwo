using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.Text;

namespace ChatTwo.Util;

public static class GlobalParametersCache
{
    private static int[] Cache = [];

    public static int GetValue(int index)
    {
        if (index < 0 || index >= Cache.Length)
            return 0;

        return Cache[index];
    }

    /// <summary>
    /// Refresh the cache of global parameters from RaptureTextModule.
    /// </summary>
    /// <remarks>
    /// This should be called in the main thread when updates are necessary.
    /// </remarks>
    public static unsafe void Refresh()
    {
        if (!ThreadSafety.IsMainThread)
            throw new InvalidOperationException("GlobalParametersCache.Refresh must be called on the main thread.");

        var rtm = RaptureTextModule.Instance();
        if (rtm is null)
            return;

        ref var gp = ref rtm->TextModule.MacroDecoder.GlobalParameters;
        if (Cache.Length != (int)gp.MySize)
            Cache = new int[gp.MySize];

        for (ulong i = 0; i < gp.MySize; i++)
        {
            var p = gp[(long)i];
            if (p.Type == TextParameterType.Integer)
                Cache[(int)i] = p.IntValue;
            else
                Cache[(int)i] = 0;
        }
    }
}