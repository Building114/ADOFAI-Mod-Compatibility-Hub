using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;

namespace KeyViewer.Patches;

public static class GameLifecyclePatch {
    public static void Apply(Harmony harmony) {
        if(harmony == null) return;

        TryPatch(harmony, "scnGame", "Play", postfix: nameof(OnGameplayStarted));
        TryPatch(harmony, "scrPressToStart", "ShowText", postfix: nameof(OnGameplayStarted));
        TryPatch(harmony, "scnGame", "ResetScene", postfix: nameof(OnGameplayRestarted));
        TryPatch(harmony, "scnEditor", "ResetScene", postfix: nameof(OnGameplayStopped));
        TryPatch(harmony, "scrController", "StartLoadingScene", prefix: nameof(OnGameplayStopped));
        TryPatch(harmony, "scrUIController", "WipeToBlack", prefix: nameof(OnGameplayStopped));
        TryPatch(harmony, "StateBehaviour", "ChangeState", postfix: nameof(OnStateChanged));
    }

    private static void TryPatch(Harmony harmony, string typeName, string methodName, string prefix = null, string postfix = null) {
        try {
            Type type = AccessTools.TypeByName(typeName);
            if(type == null) {
                Main.Logger?.Log($"[r141 lifecycle] Skip {typeName}.{methodName}: type not found");
                return;
            }

            MethodBase target = AccessTools.Method(type, methodName)
                ?? type.GetMethods(AccessTools.all).FirstOrDefault(m => m.Name == methodName);

            if(target == null) {
                Main.Logger?.Log($"[r141 lifecycle] Skip {typeName}.{methodName}: method not found");
                return;
            }

            HarmonyMethod prefixMethod = prefix == null ? null : new HarmonyMethod(typeof(GameLifecyclePatch).GetMethod(prefix, AccessTools.all));
            HarmonyMethod postfixMethod = postfix == null ? null : new HarmonyMethod(typeof(GameLifecyclePatch).GetMethod(postfix, AccessTools.all));
            harmony.Patch(target, prefixMethod, postfixMethod);
            Main.Logger?.Log($"[r141 lifecycle] Patched {type.FullName}.{target.Name}");
        } catch(Exception ex) {
            Main.Logger?.Log($"[r141 lifecycle] Failed {typeName}.{methodName}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void OnGameplayStarted() => Main.MarkGameplayStarted();
    private static void OnGameplayStopped() => Main.MarkGameplayStopped();

    private static void OnGameplayRestarted() {
        Main.MarkGameplayStarted();
        Main.ResetKeys();
    }

    private static void OnStateChanged(object __0 = null) {
        string state = __0?.ToString() ?? string.Empty;
        if(state.IndexOf("dead", StringComparison.OrdinalIgnoreCase) >= 0
            || state.IndexOf("fail", StringComparison.OrdinalIgnoreCase) >= 0
            || state.IndexOf("clear", StringComparison.OrdinalIgnoreCase) >= 0
            || state.IndexOf("complete", StringComparison.OrdinalIgnoreCase) >= 0
            || state.IndexOf("editor", StringComparison.OrdinalIgnoreCase) >= 0) {
            Main.MarkGameplayEnded();
        }
    }
}
