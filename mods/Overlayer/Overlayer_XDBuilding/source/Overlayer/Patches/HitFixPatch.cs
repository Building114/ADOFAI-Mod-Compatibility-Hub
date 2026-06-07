using HarmonyLib;
using Overlayer.Core.Patches;
using Overlayer.Utils;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Overlayer.Patches;

public class HitFixPatch : SafeConditionalPatch {
    public HitFixPatch() : base(nameof(HitFixPatch)) { }

    protected override bool ShouldApply() => Main.Settings.ShowTrueAutoJudgment;

    protected override MethodBase GetTargetMethod() =>
        VersionSafe.ReleaseNumber >= 141
            ? SafePatch.GetMethodSafe("scrPlanet", "SwitchChosen")
            : SafePatch.GetMethodSafe("scrController", "Hit");

    protected override HarmonyMethod Transpiler() =>
        new(typeof(HitFixPatch).GetMethod(nameof(TranspilerImpl), BindingFlags.Static | BindingFlags.NonPublic));

    private static IEnumerable<CodeInstruction> TranspilerImpl(IEnumerable<CodeInstruction> instructions) {
        var list = new List<CodeInstruction>(instructions);

        for(int i = 0; i < list.Count; i++) {
            if(IsAutoGetterCall(list[i])) {
                RemoveScrPlayerInstanceLoadIfNeeded(list, i);
                list[i].opcode = OpCodes.Ldc_I4_0;
                list[i].operand = null;
            }
        }

        return list;
    }

    private static bool IsAutoGetterCall(CodeInstruction instruction) {
        if(instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt) {
            return false;
        }

        return instruction.operand is MethodInfo method && method.Name == "get_auto";
    }

    private static void RemoveScrPlayerInstanceLoadIfNeeded(List<CodeInstruction> list, int callIndex) {
        if(callIndex < 2) {
            return;
        }

        if(list[callIndex].operand is not MethodInfo method || method.DeclaringType?.Name != "scrPlayer") {
            return;
        }

        // r141/r142 uses: ldarg.0 -> ldfld player -> callvirt scrPlayer.get_auto().
        // After replacing callvirt with ldc.i4.0, that scrPlayer instance must be removed
        // from the stack, otherwise the patched method will break at runtime.
        if(list[callIndex - 1].opcode == OpCodes.Ldfld &&
           list[callIndex - 1].operand is FieldInfo field &&
           field.Name == "player") {
            list[callIndex - 1].opcode = OpCodes.Nop;
            list[callIndex - 1].operand = null;

            if(list[callIndex - 2].opcode == OpCodes.Ldarg_0) {
                list[callIndex - 2].opcode = OpCodes.Nop;
                list[callIndex - 2].operand = null;
            }
        }
    }
}
