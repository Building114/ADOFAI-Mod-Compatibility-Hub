using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityModManagerNet;
using static Settings;

namespace TimingShow
{
    public static class Main
    {
        public static UnityModManager.ModEntry.ModLogger Logger;
        public static bool IsEnabled;
        public static Settings Settings;
        public static double LastTiming = 0;
        public static Color LastTimingColor = Color.white;
        public static GameObject hudObject;
        public static TextUI hudInstance;

        // 旧字段保留，避免已有逻辑和旧配置被破坏；多人模式用下面的数组分开记。
        public static List<double> SessionOffsets = new List<double>();
        public const int MaxLocalPlayers = 4;
        public static readonly double[] LastTimingByPlayer = new double[MaxLocalPlayers];
        public static readonly Color[] LastTimingColorByPlayer = CreateColorBuckets();
        public static readonly List<double>[] SessionOffsetsByPlayer = CreateSessionOffsetBuckets();
        public static int LastHitPlayerIndex = 0;

        private class PendingHitTextReplacement
        {
            public int PlayerIndex;
            public string MarginName;
            public string ReplacementText;
            public float ApplyUntil;
            public int RemainingExtraWrites;
            public int RemainingReadDelayFrames;
            public int InitialReadDelayFrames;
            public bool PreciseReadDone;
            public int ApplyCount;
            public List<object> TextObjects;
        }

        private static readonly Dictionary<object, PendingHitTextReplacement> PendingHitTextReplacements =
            new Dictionary<object, PendingHitTextReplacement>();

        public static int DebugShowCalls;
        public static int DebugApplyAttempts;
        public static int DebugApplySuccess;
        public static int DebugApplyFailed;
        public static string DebugLastShowMethod = "-";
        public static string DebugLastStatus = "-";
        public static string DebugLastMargin = "-";
        public static string DebugLastColorSource = "-";
        public static string DebugLastTextTarget = "-";
        public static string DebugLastReplacement = "-";
        public static int DebugLastPlayerIndex;
        public static int DebugLastTextObjectCount;

        public static string L(string zh, string en) => Settings.Language == 0 ? zh : en;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Logger = modEntry.Logger;
            Settings = UnityModManager.ModSettings.Load<Settings>(modEntry);

            modEntry.OnToggle = (entry, value) =>
            {
                IsEnabled = value;
                if (!value)
                {
                    ResetTimingState();
                    if (hudObject != null)
                        hudObject.SetActive(false);
                }
                return true;
            };

            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;

            try
            {
                new Harmony(modEntry.Info.Id).PatchAll();
                Logger.Log("TimingShow r143 compatibility patches loaded.");
            }
            catch (Exception ex)
            {
                Logger.Error("TimingShow failed to patch: " + ex);
            }

            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            GUILayout.BeginHorizontal();
            GUIStyle zhStyle = new GUIStyle(GUI.skin.button);
            if (Settings.Language == 0) zhStyle.fontStyle = FontStyle.Bold;
            if (GUILayout.Button("简体中文", zhStyle, GUILayout.Width(100))) Settings.Language = 0;
            GUIStyle enStyle = new GUIStyle(GUI.skin.button);
            if (Settings.Language == 1) enStyle.fontStyle = FontStyle.Bold;
            if (GUILayout.Button("English", enStyle, GUILayout.Width(100))) Settings.Language = 1;

            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            DrawSettingRow(L(Locale_zh.Toggle_Title, Locale_en.Toggle_Title), ref Settings.ShowInSongTitle, ref Settings.Perc1);
            if (Settings.ShowInSongTitle)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                Settings.Title_UseJudgeColor = GUILayout.Toggle(Settings.Title_UseJudgeColor, L(Locale_zh.HUD_UseJudgeColor, Locale_en.HUD_UseJudgeColor));
                GUILayout.EndHorizontal();
            }

            DrawSettingRow(L(Locale_zh.Toggle_Planet, Locale_en.Toggle_Planet), ref Settings.ShowOnPlanet, ref Settings.Perc2);
            if (Settings.ShowOnPlanet)
            {
                GUILayout.Label(L(Locale_zh.Setting_Title, Locale_en.Setting_Title));

                Settings.ReplaceFailOverload = GUILayout.Toggle(Settings.ReplaceFailOverload, L(Locale_zh.Toggle_FailOverload, Locale_en.Toggle_FailOverload));
                Settings.ReplaceTooEarly = GUILayout.Toggle(Settings.ReplaceTooEarly, L(Locale_zh.Toggle_TooEarly, Locale_en.Toggle_TooEarly));
                Settings.ReplaceVeryEarly = GUILayout.Toggle(Settings.ReplaceVeryEarly, L(Locale_zh.Toggle_VeryEarly, Locale_en.Toggle_VeryEarly));
                Settings.ReplaceEarlyPerfect = GUILayout.Toggle(Settings.ReplaceEarlyPerfect, L(Locale_zh.Toggle_EarlyPerfect, Locale_en.Toggle_EarlyPerfect));
                Settings.ReplacePerfect = GUILayout.Toggle(Settings.ReplacePerfect, L(Locale_zh.Toggle_Perfect, Locale_en.Toggle_Perfect));
                Settings.ReplaceLatePerfect = GUILayout.Toggle(Settings.ReplaceLatePerfect, L(Locale_zh.Toggle_LatePerfect, Locale_en.Toggle_LatePerfect));
                Settings.ReplaceVeryLate = GUILayout.Toggle(Settings.ReplaceVeryLate, L(Locale_zh.Toggle_VeryLate, Locale_en.Toggle_VeryLate));
                Settings.ReplaceTooLate = GUILayout.Toggle(Settings.ReplaceTooLate, L(Locale_zh.Toggle_TooLate, Locale_en.Toggle_TooLate));
                Settings.ReplaceFailMiss = GUILayout.Toggle(Settings.ReplaceFailMiss, L(Locale_zh.Toggle_FailMiss, Locale_en.Toggle_FailMiss));
                Settings.ReplaceMultipress = GUILayout.Toggle(Settings.ReplaceMultipress, L(Locale_zh.Toggle_Multipress, Locale_en.Toggle_Multipress));
            }

            DrawSettingRow(L(Locale_zh.Toggle_Death, Locale_en.Toggle_Death), ref Settings.ShowOnDeath, ref Settings.Perc3);
            DrawSettingRow(L(Locale_zh.Toggle_Win, Locale_en.Toggle_Win), ref Settings.ShowInWinPage, ref Settings.Perc4);

            Settings.ShowTimingHUD = GUILayout.Toggle(Settings.ShowTimingHUD, L(Locale_zh.Toggle_TimingHUD, Locale_en.Toggle_TimingHUD));
            if (Settings.ShowTimingHUD)
            {
                GUILayout.Label(L(Locale_zh.Title_TimingHUD, Locale_en.Title_TimingHUD));

                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUILayout.Label(L(Locale_zh.Label_XOffset, Locale_en.Label_XOffset) + $"{Settings.HUD_x:F2}", GUILayout.Width(120));
                Settings.HUD_x = GUILayout.HorizontalSlider(Settings.HUD_x, -0.5f, 0.5f, GUILayout.Width(120));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUILayout.Label(L(Locale_zh.Label_YOffset, Locale_en.Label_YOffset) + $"{Settings.HUD_y:F2}", GUILayout.Width(120));
                Settings.HUD_y = GUILayout.HorizontalSlider(Settings.HUD_y, -0.5f, 0.5f, GUILayout.Width(120));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUILayout.Label(L(Locale_zh.Label_Scale, Locale_en.Label_Scale) + $"{Settings.HUD_scale:F2}", GUILayout.Width(120));
                Settings.HUD_scale = GUILayout.HorizontalSlider(Settings.HUD_scale, 0.2f, 3.0f, GUILayout.Width(120));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                Settings.HUD_bold = GUILayout.Toggle(Settings.HUD_bold, L(Locale_zh.Toggle_Bold, Locale_en.Toggle_Bold));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUILayout.Label(L(Locale_zh.Label_Align, Locale_en.Label_Align), GUILayout.Width(100));

                GUIStyle leftStyle = new GUIStyle(GUI.skin.button);
                if (Settings.HUD_align == 0) leftStyle.fontStyle = FontStyle.Bold;
                if (GUILayout.Button(L(Locale_zh.Btn_Left, Locale_en.Btn_Left), leftStyle, GUILayout.Width(60))) Settings.HUD_align = 0;

                GUIStyle centerStyle = new GUIStyle(GUI.skin.button);
                if (Settings.HUD_align == 1) centerStyle.fontStyle = FontStyle.Bold;
                if (GUILayout.Button(L(Locale_zh.Btn_Center, Locale_en.Btn_Center), centerStyle, GUILayout.Width(60))) Settings.HUD_align = 1;

                GUIStyle rightStyle = new GUIStyle(GUI.skin.button);
                if (Settings.HUD_align == 2) rightStyle.fontStyle = FontStyle.Bold;
                if (GUILayout.Button(L(Locale_zh.Btn_Right, Locale_en.Btn_Right), rightStyle, GUILayout.Width(60))) Settings.HUD_align = 2;
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUILayout.Label(L(Locale_zh.Label_Format, Locale_en.Label_Format), GUILayout.Width(100));
                Settings.HUD_Format = GUILayout.TextField(Settings.HUD_Format, GUILayout.Width(200));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUILayout.Label(L(Locale_zh.Label_Precision, Locale_en.Label_Precision) + $"{Settings.PercHUD}", GUILayout.Width(120));
                Settings.PercHUD = Mathf.RoundToInt(GUILayout.HorizontalSlider(Settings.PercHUD, 0, 5, GUILayout.Width(100)));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                Settings.HUD_UseJudgeColor = GUILayout.Toggle(Settings.HUD_UseJudgeColor, L(Locale_zh.HUD_UseJudgeColor, Locale_en.HUD_UseJudgeColor));
                GUILayout.EndHorizontal();
            }

            if (Settings.ShowOnPlanet)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUILayout.Label(L(Locale_zh.Label_HitTextExtraRewriteCount, Locale_en.Label_HitTextExtraRewriteCount) + Settings.HitTextExtraRewriteCount, GUILayout.Width(180));
                Settings.HitTextExtraRewriteCount = Mathf.RoundToInt(GUILayout.HorizontalSlider(Settings.HitTextExtraRewriteCount, 0, 2, GUILayout.Width(100)));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUILayout.Label(L(Locale_zh.Label_HitTextReadDelayFrames, Locale_en.Label_HitTextReadDelayFrames) + Settings.HitTextReadDelayFrames, GUILayout.Width(180));
                Settings.HitTextReadDelayFrames = Mathf.RoundToInt(GUILayout.HorizontalSlider(Settings.HitTextReadDelayFrames, 0, 3, GUILayout.Width(100)));
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);
            Settings.ShowDebugPanel = GUILayout.Toggle(Settings.ShowDebugPanel, L(Locale_zh.Toggle_DebugPanel, Locale_en.Toggle_DebugPanel));
            if (Settings.ShowDebugPanel)
                DrawDebugPanel();

            GUILayout.Space(15);
            if (GUILayout.Button(L(Locale_zh.Btn_Reset, Locale_en.Btn_Reset), GUILayout.Width(150)))
            {
                ResetTimingState();
            }
        }

        static void DrawDebugPanel()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(BuildDebugInfo());
            if (GUILayout.Button(L(Locale_zh.Btn_ClearDebug, Locale_en.Btn_ClearDebug), GUILayout.Width(150)))
                ClearDebugInfo();
            GUILayout.EndVertical();
        }

        static void DrawSettingRow(string label, ref bool toggle, ref int precision)
        {
            toggle = GUILayout.Toggle(toggle, label);
            if (toggle)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                string precisionLabel = L(Locale_zh.Label_Precision, Locale_en.Label_Precision);
                GUILayout.Label($"{precisionLabel}{precision}", GUILayout.Width(120));
                precision = Mathf.RoundToInt(GUILayout.HorizontalSlider(precision, 0, 5, GUILayout.Width(100)));
                GUILayout.EndHorizontal();
            }
        }

        static void OnSaveGUI(UnityModManager.ModEntry modEntry) => Settings.Save(modEntry);

        private static Color[] CreateColorBuckets()
        {
            Color[] colors = new Color[MaxLocalPlayers];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = Color.white;
            return colors;
        }

        private static List<double>[] CreateSessionOffsetBuckets()
        {
            List<double>[] buckets = new List<double>[MaxLocalPlayers];
            for (int i = 0; i < buckets.Length; i++)
                buckets[i] = new List<double>();
            return buckets;
        }

        public static int NormalizePlayerIndex(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= MaxLocalPlayers)
                return 0;
            return playerIndex;
        }

        public static void ResetTimingState()
        {
            SessionOffsets.Clear();
            PendingHitTextReplacements.Clear();
            LastTiming = 0;
            LastTimingColor = Color.white;
            LastHitPlayerIndex = 0;
            for (int i = 0; i < MaxLocalPlayers; i++)
            {
                LastTimingByPlayer[i] = 0;
                LastTimingColorByPlayer[i] = Color.white;
                SessionOffsetsByPlayer[i].Clear();
            }
        }

        public static void SetLastTiming(int playerIndex, double timing)
        {
            playerIndex = NormalizePlayerIndex(playerIndex);
            LastTimingByPlayer[playerIndex] = timing;
            LastTiming = timing;
            LastHitPlayerIndex = playerIndex;
        }

        public static double GetLastTiming(int playerIndex)
        {
            return LastTimingByPlayer[NormalizePlayerIndex(playerIndex)];
        }

        public static void SetLastTimingColor(int playerIndex, Color color)
        {
            playerIndex = NormalizePlayerIndex(playerIndex);
            LastTimingColorByPlayer[playerIndex] = color;
            LastTimingColor = color;
            LastHitPlayerIndex = playerIndex;
        }

        public static Color GetLastTimingColor(int playerIndex)
        {
            return LastTimingColorByPlayer[NormalizePlayerIndex(playerIndex)];
        }

        public static void RegisterPreciseHitTextReplacement(object hitText, object textObject, int playerIndex, string marginName, string showMethod, string colorSource)
        {
            if (hitText == null)
                return;

            DebugShowCalls++;
            playerIndex = NormalizePlayerIndex(playerIndex);
            DebugLastShowMethod = string.IsNullOrEmpty(showMethod) ? "-" : showMethod;
            DebugLastPlayerIndex = playerIndex;
            DebugLastMargin = string.IsNullOrEmpty(marginName) ? "-" : marginName;

            if (!Settings.ShowOnPlanet || !Compat.ShouldReplaceMargin(marginName, Settings))
            {
                PendingHitTextReplacements.Remove(hitText);
                DebugLastStatus = "skip: setting or margin";
                DebugLastTextObjectCount = 0;
                DebugLastTextTarget = "-";
                DebugLastColorSource = "-";
                return;
            }

            int readDelay = Mathf.Clamp(Settings.HitTextReadDelayFrames, 0, 3);
            int extraWrites = Mathf.Clamp(Settings.HitTextExtraRewriteCount, 0, 2);

            List<object> textObjects = new List<object>();
            if (textObject != null)
                textObjects.Add(textObject);

            // delay > 0 时允许 mainText 暂时为空，等延迟帧结束后再读一次。
            if (textObjects.Count == 0 && readDelay == 0)
            {
                PendingHitTextReplacements.Remove(hitText);
                DebugLastStatus = "skip: mainText not found";
                DebugLastTextObjectCount = 0;
                DebugLastTextTarget = "-";
                DebugLastColorSource = "-";
                return;
            }

            PendingHitTextReplacement pending = new PendingHitTextReplacement();
            pending.PlayerIndex = playerIndex;
            pending.MarginName = marginName ?? "";
            pending.ReplacementText = Format(GetLastTiming(playerIndex), Settings.Perc2);
            pending.ApplyUntil = Time.unscaledTime + 0.25f + readDelay * 0.12f;
            pending.RemainingExtraWrites = extraWrites;
            pending.RemainingReadDelayFrames = readDelay;
            pending.InitialReadDelayFrames = readDelay;
            pending.PreciseReadDone = false;
            pending.ApplyCount = 0;
            pending.TextObjects = textObjects;
            PendingHitTextReplacements[hitText] = pending;

            DebugLastTextObjectCount = textObjects.Count;
            DebugLastTextTarget = Compat.DescribeTextObjects(textObjects);
            DebugLastReplacement = pending.ReplacementText;
            DebugLastColorSource = readDelay == 0 ? "pending read now" : "pending read after " + readDelay + " frame(s)";
            DebugLastStatus = readDelay == 0 ? "registered precise" : "registered precise, wait " + readDelay + " frame(s)";
        }

        public static void RegisterHitTextReplacement(object hitText, int playerIndex, string marginName, string showMethod, string colorSource)
        {
            object textObject = Compat.GetPreciseHitTextObject(hitText);
            RegisterPreciseHitTextReplacement(hitText, textObject, playerIndex, marginName, showMethod, colorSource);
        }

        public static bool TryApplyPendingHitTextReplacement(object hitText, bool immediate)
        {
            if (!IsEnabled || hitText == null)
                return false;

            PendingHitTextReplacement pending;
            if (!PendingHitTextReplacements.TryGetValue(hitText, out pending))
                return false;

            if (!Settings.ShowOnPlanet || Time.unscaledTime > pending.ApplyUntil || !Compat.IsActiveHitTextObject(hitText))
            {
                PendingHitTextReplacements.Remove(hitText);
                DebugLastStatus = "removed: expired/inactive";
                return false;
            }

            if (!pending.PreciseReadDone)
            {
                if (pending.RemainingReadDelayFrames > 0)
                {
                    if (immediate)
                    {
                        DebugLastStatus = "wait read delay " + pending.RemainingReadDelayFrames + " frame(s)";
                        return false;
                    }

                    pending.RemainingReadDelayFrames--;
                    if (pending.RemainingReadDelayFrames > 0)
                    {
                        DebugLastStatus = "wait read delay " + pending.RemainingReadDelayFrames + " frame(s)";
                        return false;
                    }
                }

                if (pending.TextObjects == null)
                    pending.TextObjects = new List<object>();

                if (pending.TextObjects.Count == 0)
                {
                    object delayedTextObject = Compat.GetPreciseHitTextObject(hitText);
                    if (delayedTextObject != null)
                        pending.TextObjects.Add(delayedTextObject);
                }

                DebugLastTextObjectCount = pending.TextObjects.Count;
                DebugLastTextTarget = Compat.DescribeTextObjects(pending.TextObjects);

                if (pending.TextObjects.Count == 0)
                {
                    PendingHitTextReplacements.Remove(hitText);
                    DebugLastStatus = "skip: mainText not found after delay";
                    return false;
                }

                Color judgeColor;
                string colorSource;
                object textObject = pending.TextObjects[0];
                if (Compat.TryGetPreciseHitTextColor(hitText, textObject, pending.MarginName, out judgeColor, out colorSource))
                {
                    SetLastTimingColor(pending.PlayerIndex, judgeColor);
                    DebugLastColorSource = colorSource;
                }
                else
                {
                    DebugLastColorSource = "not found";
                }

                pending.PreciseReadDone = true;
            }

            // 第一次真正写入不算“补写”。延迟读取时，第一次写入可能发生在 LateUpdate。
            if (!immediate && pending.ApplyCount > 0)
            {
                if (pending.RemainingExtraWrites <= 0)
                {
                    PendingHitTextReplacements.Remove(hitText);
                    return false;
                }
                pending.RemainingExtraWrites--;
            }

            DebugApplyAttempts++;
            bool changed = Compat.ApplyHitTextReplacement(pending.TextObjects, pending.ReplacementText);
            pending.ApplyCount++;

            if (changed)
                DebugApplySuccess++;
            else
                DebugApplyFailed++;

            DebugLastReplacement = pending.ReplacementText;

            string writeKind;
            if (pending.InitialReadDelayFrames > 0 && pending.ApplyCount == 1)
                writeKind = "delayed write";
            else
                writeKind = immediate ? "show write" : "extra write";

            DebugLastStatus = writeKind + " #" + pending.ApplyCount
                              + " readDelay " + pending.InitialReadDelayFrames
                              + " remainExtra " + pending.RemainingExtraWrites;

            if (pending.RemainingExtraWrites <= 0)
                PendingHitTextReplacements.Remove(hitText);

            return changed;
        }

        public static bool TryApplyPendingHitTextReplacement(object hitText)
        {
            return TryApplyPendingHitTextReplacement(hitText, false);
        }

        public static string BuildDebugInfo()
        {
            StringBuilder sb = new StringBuilder(512);
            sb.AppendLine("TimingShow Debug");
            sb.AppendLine("Show calls: " + DebugShowCalls + " | writes: " + DebugApplySuccess + " ok / " + DebugApplyFailed + " failed / " + DebugApplyAttempts + " tries");
            sb.AppendLine("Pending: " + PendingHitTextReplacements.Count
                          + " | read delay: " + Mathf.Clamp(Settings.HitTextReadDelayFrames, 0, 3)
                          + " | extra rewrite limit: " + Mathf.Clamp(Settings.HitTextExtraRewriteCount, 0, 2)
                          + " | mode: precise fields");
            sb.AppendLine("Last method: " + DebugLastShowMethod);
            sb.AppendLine("Last player: P" + (DebugLastPlayerIndex + 1) + " | margin: " + DebugLastMargin + " | color: " + DebugLastColorSource);
            sb.AppendLine("Text objects: " + DebugLastTextObjectCount + " | " + DebugLastTextTarget);
            sb.AppendLine("Replacement: " + DebugLastReplacement + " | status: " + DebugLastStatus);

            int playerCount = Compat.GetPlayerCount();
            if (playerCount > MaxLocalPlayers)
                playerCount = MaxLocalPlayers;
            for (int i = 0; i < playerCount; i++)
            {
                sb.Append("P").Append(i + 1)
                  .Append(": ").Append(LastTimingByPlayer[i].ToString("F" + Settings.Perc2)).Append("ms")
                  .Append(" color #").Append(ColorUtility.ToHtmlStringRGB(LastTimingColorByPlayer[i]));
                if (i + 1 < playerCount)
                    sb.AppendLine();
            }

            return sb.ToString();
        }

        public static void ClearDebugInfo()
        {
            DebugShowCalls = 0;
            DebugApplyAttempts = 0;
            DebugApplySuccess = 0;
            DebugApplyFailed = 0;
            DebugLastShowMethod = "-";
            DebugLastStatus = "-";
            DebugLastMargin = "-";
            DebugLastColorSource = "-";
            DebugLastTextTarget = "-";
            DebugLastReplacement = "-";
            DebugLastPlayerIndex = 0;
            DebugLastTextObjectCount = 0;
        }

        public static void AddSessionOffset(int playerIndex, double timing)
        {
            SessionOffsets.Add(timing);
            SessionOffsetsByPlayer[NormalizePlayerIndex(playerIndex)].Add(timing);
        }

        public static void ClearSessionOffsets()
        {
            SessionOffsets.Clear();
            for (int i = 0; i < SessionOffsetsByPlayer.Length; i++)
                SessionOffsetsByPlayer[i].Clear();
        }

        public static string BuildTimingDisplay(int precision, bool useJudgeColor, bool singleValueWithoutMs)
        {
            int playerCount = Compat.GetPlayerCount();
            if (playerCount <= 1)
            {
                string value = LastTiming.ToString("F" + precision);
                if (!singleValueWithoutMs)
                    value += "ms";
                if (useJudgeColor)
                    value = "<color=#" + ColorUtility.ToHtmlStringRGB(LastTimingColor) + ">" + value + "</color>";
                return value;
            }

            if (playerCount > MaxLocalPlayers)
                playerCount = MaxLocalPlayers;

            StringBuilder sb = new StringBuilder(64 * playerCount);
            for (int i = 0; i < playerCount; i++)
            {
                if (i > 0)
                    sb.Append(" | ");

                string value = "P" + (i + 1) + " " + LastTimingByPlayer[i].ToString("F" + precision) + "ms";
                if (useJudgeColor)
                    value = "<color=#" + ColorUtility.ToHtmlStringRGB(LastTimingColorByPlayer[i]) + ">" + value + "</color>";
                sb.Append(value);
            }

            return sb.ToString();
        }

        public static string BuildAverageTimingInfo()
        {
            int playerCount = Compat.GetPlayerCount();
            string label = L(Locale_zh.Avg_Timing, Locale_en.Avg_Timing);

            if (playerCount <= 1)
                return "\n" + label + Format(GetAverage(SessionOffsets), Settings.Perc4);

            if (playerCount > MaxLocalPlayers)
                playerCount = MaxLocalPlayers;

            StringBuilder sb = new StringBuilder(96);
            sb.Append("\n").Append(label);
            bool hasAny = false;
            for (int i = 0; i < playerCount; i++)
            {
                if (SessionOffsetsByPlayer[i].Count == 0)
                    continue;

                if (hasAny)
                    sb.Append(" | ");
                sb.Append("P").Append(i + 1).Append(" ")
                  .Append(Format(GetAverage(SessionOffsetsByPlayer[i]), Settings.Perc4));
                hasAny = true;
            }

            if (!hasAny)
                sb.Append(Format(GetAverage(SessionOffsets), Settings.Perc4));

            return sb.ToString();
        }

        private static double GetAverage(List<double> values)
        {
            if (values == null || values.Count == 0)
                return 0;

            double total = 0;
            foreach (double value in values)
                total += value;
            return total / values.Count;
        }

        public static bool IsPlaying() => Compat.IsPlayerControl();

        public static string Format(double val, int precision) => $"{val.ToString("F" + precision)}ms";

        public static void UpdateHUD()
        {
            scrController controller = Compat.GetCurrentController();
            bool isplay = IsEnabled
                          && Settings.ShowTimingHUD
                          && IsPlaying()
                          && controller != null
                          && Compat.IsGameWorld()
                          && !Compat.IsPaused(controller);

            if (hudObject == null)
            {
                hudObject = new GameObject("TimingShow_HUD");
                UnityEngine.Object.DontDestroyOnLoad(hudObject);
                hudInstance = hudObject.AddComponent<TextUI>();
            }

            hudObject.SetActive(isplay);
            if (!isplay)
            {
                if (hudInstance != null)
                    hudInstance.SetText("");
                return;
            }

            bool isMultiPlayer = Compat.GetPlayerCount() > 1;
            string timing = BuildTimingDisplay(Settings.PercHUD, Settings.HUD_UseJudgeColor, true);
            string format = Settings.HUD_Format;
            if (isMultiPlayer && !string.IsNullOrEmpty(format))
                format = format.Replace("{0}ms", "{0}");

            string hudText;
            try
            {
                hudText = string.Format(format, timing);
            }
            catch
            {
                hudText = timing + "ms";
            }

            hudInstance.SetText(hudText);
            hudInstance.SetPosition(Settings.HUD_x, Settings.HUD_y);
            hudInstance.SetSize((int)(24 * Settings.HUD_scale));
            hudInstance.text.alignment = hudInstance.ToAlign(Settings.HUD_align);
            hudInstance.text.fontStyle = Settings.HUD_bold ? FontStyle.Bold : FontStyle.Normal;
        }
    }
}
