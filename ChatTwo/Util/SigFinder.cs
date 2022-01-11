using System.Reflection;
using Dalamud.Game;
using Dalamud.Logging;
using JetBrains.Annotations;

namespace ChatTwo.Util;

internal static class SigFinder {
    internal static void ScanFunctions(this SigScanner scanner, object self) {
        var selfType = self.GetType();
        var funcs = selfType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(field => (field, field.GetCustomAttribute<SignatureAttribute>()))
            .Where(tuple => tuple.Item2 != null);
        foreach (var (field, attr) in funcs) {
            if (!scanner.TryScanText(attr!.Signature, out var ptr)) {
                PluginLog.LogWarning($"Could not find signature for {selfType.Name}.{field.Name}: {attr.Signature}");
                continue;
            }

            field.SetValue(self, ptr);
        }
    }
}

[AttributeUsage(AttributeTargets.Field)]
[MeansImplicitUse(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.Itself)]
internal class SignatureAttribute : Attribute {
    internal readonly string Signature;

    internal SignatureAttribute(string signature) {
        this.Signature = signature;
    }
}
