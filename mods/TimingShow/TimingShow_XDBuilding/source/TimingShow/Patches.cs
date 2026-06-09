using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace TimingShow
{
    // timing calc
    [HarmonyPatch(typeof(scrPlanet), "SwitchChosen")]
    public static class Patches
    {
        public static void Prefix(scrPlanet __instance)
        {
            if (!Main.IsEnabled)
                return;

            try
            {
                double diff;
                if (!Compat.TryCalculateTiming(__instance, out diff))
                    return;

                int playerIndex = Compat.GetPlayerIndex(__instance);
                Main.SetLastTiming(playerIndex, diff);
                if (Main.IsPlaying() && Main.Settings.ShowInWinPage && !Compat.IsAuto())
                    Main.AddSessionOffset(playerIndex, diff);
            }
            catch (Exception ex)
            {
                if (Main.Logger != null)
                    Main.Logger.Warning("Timing calculation failed: " + ex.Message);
            }
        }
    }

    // 判定文字精准路径：
    // 现在调试已确认新版实际走 scrHitTextMesh.Show，
    // 文字对象是 mainText，对应 TextMeshPro；
    // 颜色来源是 mainText.color。
    // 所以默认不再扫描 scrHitText/scrHitTextUI/子物体/一堆可能字段。
    [HarmonyPatch(typeof(scrHitTextMesh), "Show")]
    public static class HitTextMeshShowPatch
    {
        public static void Postfix(scrHitTextMesh __instance, object[] __args)
        {
            if (!Main.IsEnabled || __instance == null)
                return;

            try
            {
                int playerIndex = Main.LastHitPlayerIndex;
                string marginName = Compat.GetPreciseHitMarginName(__instance, __args);
                object textObject = Compat.GetPreciseHitTextObject(__instance);

                // 颜色读取挪到 Main.TryApplyPendingHitTextReplacement。
                // 这样 HitTextReadDelayFrames > 0 时，能等指定帧数后再读 mainText.color。
                Main.RegisterPreciseHitTextReplacement(__instance, textObject, playerIndex, marginName, "scrHitTextMesh.Show", "pending read");
                Main.TryApplyPendingHitTextReplacement(__instance, true);
            }
            catch (Exception ex)
            {
                if (Main.Logger != null)
                    Main.Logger.Warning("Precise hit text replacement failed: " + ex.Message);
            }
        }
    }

    // 只有用户手动把补写次数调到 1~2 时才会真正写。
    // 仍然只挂 scrHitTextMesh 的刷新，不再扫其它类。
    [HarmonyPatch]
    public static class HitTextMeshKeepReplacementPatch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            MethodInfo method = AccessTools.Method(typeof(scrHitTextMesh), "LateUpdate");
            if (method == null)
                method = AccessTools.Method(typeof(scrHitTextMesh), "Update");
            if (method != null)
                yield return method;
        }

        public static void Postfix(scrHitTextMesh __instance)
        {
            if (!Main.IsEnabled || __instance == null)
                return;

            try
            {
                Main.TryApplyPendingHitTextReplacement(__instance);
            }
            catch
            {
                // 每帧兜底，不刷屏写日志。
            }
        }
    }

    // r141+ local multiplayer records hits per player in scrMarginTracker.
    // This patch only remembers which player just hit, so hit text can use it as a safe fallback.
    [HarmonyPatch]
    public static class MarginTrackerAddHitPatch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            Type trackerType = AccessTools.TypeByName("scrMarginTracker");
            MethodInfo method = trackerType == null ? null : AccessTools.Method(trackerType, "AddHit");
            if (method != null)
                yield return method;
        }

        public static void Postfix(object __instance)
        {
            if (!Main.IsEnabled || __instance == null)
                return;

            Main.LastHitPlayerIndex = Compat.GetPlayerIndexFromMarginTracker(__instance);
        }
    }

    // fail text
    [HarmonyPatch(typeof(scrController), "Fail2Action")]
    public static class Fail2ActionPatch
    {
        public static void Postfix(scrController __instance)
        {
            if (!Main.IsEnabled)
                return;

            Main.ClearSessionOffsets();
            if (!Main.Settings.ShowOnDeath)
                return;

            object txtTryCalibrating;
            if (Compat.TryGetMemberValue(__instance, "txtTryCalibrating", out txtTryCalibrating))
                Compat.SetText(txtTryCalibrating, Main.Format(Main.LastTiming, Main.Settings.Perc3), false);
        }
    }

    // finish text
    [HarmonyPatch]
    public static class WinPagePatch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            string[] methodNames =
            {
                "OnLandOnPortal",
                "WinAction",
                "LevelCompleteAction",
                "ShowResults",
                "ShowWinScreen"
            };

            Type controllerType = typeof(scrController);
            for (int i = 0; i < methodNames.Length; i++)
            {
                MethodInfo method = AccessTools.Method(controllerType, methodNames[i]);
                if (method != null)
                    yield return method;
            }

            Type playerType = AccessTools.TypeByName("scrPlayer");
            if (playerType == null)
                yield break;

            for (int i = 0; i < methodNames.Length; i++)
            {
                MethodInfo method = AccessTools.Method(playerType, methodNames[i]);
                if (method != null)
                    yield return method;
            }
        }

        public static void Postfix(object __instance)
        {
            if (!Main.IsEnabled || !Main.Settings.ShowInWinPage || Main.SessionOffsets.Count == 0)
                return;

            try
            {
                string info = Main.BuildAverageTimingInfo();
                bool appended = Compat.AppendToResultText(__instance, info);
                if (!appended)
                    appended = Compat.AppendToResultText(Compat.GetCurrentController(), info);

                // 有些新版/多人结算 UI 会晚一拍出现。没写进去就先保留统计，等下一个结算相关方法再试。
                if (appended)
                    Main.ClearSessionOffsets();
            }
            catch (Exception ex)
            {
                if (Main.Logger != null)
                    Main.Logger.Warning("Result page timing append failed: " + ex.Message);
            }
        }
    }

    // lvl name
    [HarmonyPatch(typeof(scrUIController), "Update")]
    public static class UIReplacePatch
    {
        public static void Postfix(scrUIController __instance)
        {
            if (!Main.IsEnabled)
                return;

            if (Main.Settings.ShowInSongTitle && Main.IsPlaying())
            {
                object txtLevelName;
                if (Compat.TryGetMemberValue(__instance, "txtLevelName", out txtLevelName))
                {
                    string timing = Main.BuildTimingDisplay(Main.Settings.Perc1, Main.Settings.Title_UseJudgeColor, false);

                    Compat.SetSupportRichText(txtLevelName, true);
                    Compat.SetText(txtLevelName, timing, false);
                }
            }

            if (Main.Settings.ShowTimingHUD)
                Main.UpdateHUD();
        }
    }
}
