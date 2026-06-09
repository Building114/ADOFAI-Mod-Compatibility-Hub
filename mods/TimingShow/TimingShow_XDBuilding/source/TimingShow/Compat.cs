using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace TimingShow
{
    // r143 兼容层：
    // 旧版 ADOFAI 里很多东西直接挂在 scrController 上；
    // r141+ 开始，部分数据移动到了 playerOne.planetarySystem 或 scrPlayerManager。
    // 这里用“按名字查找”的方式读字段，避免字段改名/移动时整模组直接启动失败。
    internal static class Compat
    {
        private const BindingFlags Flags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        private static readonly Dictionary<string, MemberInfo> MemberCache = new Dictionary<string, MemberInfo>();

        public static scrController GetCurrentController()
        {
            object value;
            if (TryGetMemberValue(typeof(scrController), "instance", out value) && value is scrController)
                return (scrController)value;

            if (TryGetMemberValue(typeof(scrController), "_instance", out value) && value is scrController)
                return (scrController)value;

            return null;
        }

        public static bool IsPlayerControl()
        {
            scrController controller = GetCurrentController();
            if (controller == null)
                return false;

            object state;
            if (!TryGetMemberValue(controller, "state", out state) || state == null)
                return false;

            string stateText = state.ToString();
            return string.Equals(stateText, "PlayerControl", StringComparison.OrdinalIgnoreCase)
                   || stateText.IndexOf("PlayerControl", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsPaused(scrController controller)
        {
            object value;
            return TryGetMemberValue(controller, "paused", out value) && ToBool(value, false);
        }

        public static bool IsGameWorld()
        {
            object conductor;
            if (!TryGetMemberValue(typeof(scrConductor), "instance", out conductor) || conductor == null)
                return true;

            object value;
            if (!TryGetMemberValue(conductor, "isGameWorld", out value))
                return true;

            return ToBool(value, true);
        }

        public static bool IsAuto()
        {
            object value;
            return TryGetMemberValue(typeof(RDC), "auto", out value) && ToBool(value, false);
        }

        public static int GetPlayerCount()
        {
            Type playerManagerType = AccessTools.TypeByName("scrPlayerManager");
            object value;
            if (playerManagerType != null && TryGetMemberValue(playerManagerType, "playerCount", out value))
                return ClampPlayerCount(ToInt(value, 1));

            scrController controller = GetCurrentController();
            if (controller != null && TryGetMemberValue(controller, "coopMode", out value) && ToBool(value, false))
                return 2;

            return 1;
        }

        public static int GetPlayerIndex(object target)
        {
            if (target == null)
                return Main.NormalizePlayerIndex(Main.LastHitPlayerIndex);

            object value;
            string[] paths =
            {
                "player.playerID",
                "player.playerId",
                "player.playerIndex",
                "playerID",
                "playerId",
                "playerIndex",
                "index",
                "ownerPlayerIndex",
                "localPlayerIndex",
                "planet.player.playerID",
                "planet.player.playerId",
                "planetarySystem.player.playerID",
                "planetarySystem.player.playerId",
                "planetarySystem.playerID",
                "planetarySystem.playerId"
            };

            for (int i = 0; i < paths.Length; i++)
            {
                if (TryGetPath(target, paths[i], out value))
                    return ClampPlayerIndex(ToInt(value, Main.LastHitPlayerIndex));
            }

            object tracker;
            if ((TryGetMemberValue(target, "marginTracker", out tracker) ||
                 TryGetMemberValue(target, "tracker", out tracker)) && tracker != null)
                return GetPlayerIndexFromMarginTracker(tracker);

            object planet;
            if ((TryGetMemberValue(target, "planet", out planet) ||
                 TryGetMemberValue(target, "scrPlanet", out planet)) && planet != null && !ReferenceEquals(planet, target))
                return GetPlayerIndex(planet);

            return Main.NormalizePlayerIndex(Main.LastHitPlayerIndex);
        }

        public static int GetPlayerIndexFromMarginTracker(object marginTracker)
        {
            if (marginTracker == null)
                return Main.NormalizePlayerIndex(Main.LastHitPlayerIndex);

            object value;
            if (TryGetPath(marginTracker, "player.playerID", out value) ||
                TryGetPath(marginTracker, "player.playerId", out value) ||
                TryGetMemberValue(marginTracker, "playerID", out value) ||
                TryGetMemberValue(marginTracker, "playerId", out value) ||
                TryGetMemberValue(marginTracker, "playerIndex", out value))
                return ClampPlayerIndex(ToInt(value, Main.LastHitPlayerIndex));

            Type mistakesType = AccessTools.TypeByName("scrMistakesManager");
            object trackers;
            if (mistakesType != null && TryGetMemberValue(mistakesType, "marginTrackers", out trackers) && trackers is IEnumerable)
            {
                int index = 0;
                foreach (object tracker in (IEnumerable)trackers)
                {
                    if (ReferenceEquals(tracker, marginTracker))
                        return ClampPlayerIndex(index);
                    index++;
                }
            }

            return Main.NormalizePlayerIndex(Main.LastHitPlayerIndex);
        }

        private static int ClampPlayerCount(int count)
        {
            if (count < 1)
                return 1;
            if (count > Main.MaxLocalPlayers)
                return Main.MaxLocalPlayers;
            return count;
        }

        private static int ClampPlayerIndex(int index)
        {
            int count = GetPlayerCount();
            if (index < 0)
                return 0;
            if (index >= count)
                return count - 1;
            return index;
        }

        public static bool TryCalculateTiming(scrPlanet planet, out double timing)
        {
            timing = 0;
            if (planet == null)
                return false;

            scrController controller = GetCurrentController();
            if (controller == null)
                return false;

            object angleObj;
            object targetObj;
            if (!TryGetMemberValue(planet, "angle", out angleObj))
                return false;
            if (!TryGetMemberValue(planet, "targetExitAngle", out targetObj))
                return false;

            double angle = ToDouble(angleObj, double.NaN);
            double targetExitAngle = ToDouble(targetObj, double.NaN);
            if (double.IsNaN(angle) || double.IsNaN(targetExitAngle))
                return false;

            double bpm = GetBpm(planet);
            double speed = GetPlanetSpeed(controller, planet);
            double pitch = GetPitch(planet);
            if (Math.Abs(bpm) < 0.0001 || Math.Abs(speed) < 0.0001 || Math.Abs(pitch) < 0.0001)
                return false;

            bool isClockwise = GetClockwise(controller, planet);
            timing = (angle - targetExitAngle) * (isClockwise ? 1.0 : -1.0)
                     * 60000.0 / (Math.PI * bpm * speed * pitch);
            return true;
        }

        public static double GetPlanetSpeed(scrController controller, scrPlanet planet)
        {
            object value;

            // 多人模式下每个玩家都有自己的 planetarySystem，先从触发判定的 planet 取。
            if (TryGetPath(planet, "planetarySystem.speed", out value))
                return ToDouble(value, 1.0);

            if (TryGetPath(planet, "player.planetarySystem.speed", out value))
                return ToDouble(value, 1.0);

            // r141+ 单人路径：JipperResourcePack 的 VersionSafe 使用 controller.playerOne.planetarySystem.speed。
            if (TryGetPath(controller, "playerOne.planetarySystem.speed", out value))
                return ToDouble(value, 1.0);

            // 旧版：scrController.speed。
            if (TryGetMemberValue(controller, "speed", out value))
                return ToDouble(value, 1.0);

            return 1.0;
        }

        public static bool GetClockwise(scrController controller, scrPlanet planet)
        {
            object value;

            if (TryGetPath(planet, "planetarySystem.isCW", out value))
                return ToBool(value, true);

            if (TryGetPath(planet, "planetarySystem.isClockwise", out value))
                return ToBool(value, true);

            if (TryGetPath(planet, "player.planetarySystem.isCW", out value))
                return ToBool(value, true);

            if (TryGetPath(planet, "player.planetarySystem.isClockwise", out value))
                return ToBool(value, true);

            if (TryGetPath(controller, "playerOne.planetarySystem.isCW", out value))
                return ToBool(value, true);

            if (TryGetPath(controller, "playerOne.planetarySystem.isClockwise", out value))
                return ToBool(value, true);

            if (TryGetMemberValue(controller, "isCW", out value))
                return ToBool(value, true);

            if (TryGetMemberValue(planet, "isCW", out value))
                return ToBool(value, true);

            return true;
        }

        public static double GetBpm(scrPlanet planet)
        {
            object value;
            if (TryGetPath(planet, "conductor.bpm", out value))
                return ToDouble(value, 0.0);

            object conductor;
            if (TryGetMemberValue(typeof(scrConductor), "instance", out conductor) &&
                TryGetMemberValue(conductor, "bpm", out value))
                return ToDouble(value, 0.0);

            return 0.0;
        }

        public static double GetPitch(scrPlanet planet)
        {
            object value;
            if (TryGetPath(planet, "conductor.song.pitch", out value))
                return ToDouble(value, 1.0);

            object conductor;
            if (TryGetMemberValue(typeof(scrConductor), "instance", out conductor) &&
                TryGetPath(conductor, "song.pitch", out value))
                return ToDouble(value, 1.0);

            return 1.0;
        }

        public static string GetPreciseHitMarginName(object hitText, object[] args)
        {
            // 精准模式下，先读 Show 参数，因为截图已经确认 scrHitTextMesh.Show 能给出 Perfect。
            string value;
            if (TryGetMarginNameFromArgs(args, out value))
                return value;

            // 必要兜底：只试 scrHitTextMesh 本体上最常见的少量字段，不再扫文字对象。
            object memberValue;
            if (TryGetMemberValue(hitText, "hitMargin", out memberValue) && memberValue != null)
                return memberValue.ToString();

            if (TryGetMemberValue(hitText, "margin", out memberValue) && memberValue != null)
                return memberValue.ToString();

            return "";
        }

        public static object GetPreciseHitTextObject(object hitText)
        {
            if (hitText == null)
                return null;

            // 新版真实字段：scrHitTextMesh.mainText。
            // 调试截图里颜色来源也是 mainText.color。
            object value;
            if (TryGetMemberValue(hitText, "mainText", out value) && value != null)
                return value;

            // 小兜底：旧版可能叫 text / textMesh。只试两个，不扫子物体。
            if (TryGetMemberValue(hitText, "text", out value) && value != null)
                return value;

            if (TryGetMemberValue(hitText, "textMesh", out value) && value != null)
                return value;

            return null;
        }

        public static bool TryGetPreciseHitTextColor(object hitText, object textObject, string marginName, out Color color, out string source)
        {
            object value;

            // 新版真实路径：mainText.color。
            if (textObject != null && TryGetMemberValue(textObject, "color", out value) && TryConvertToColor(value, out color))
            {
                source = "mainText.color";
                return true;
            }

            // 必要兜底：本体 color。
            if (TryGetMemberValue(hitText, "color", out value) && TryConvertToColor(value, out color))
            {
                source = "hitText.color";
                return true;
            }

            // 最后才按判定名给默认色，不做字段穷举。
            if (TryGetDefaultMarginColor(marginName, out color))
            {
                source = "default margin color";
                return true;
            }

            color = Color.white;
            source = "not found";
            return false;
        }

        public static string GetHitMarginName(object hitText, object[] args)
        {
            string value;

            if (TryGetMarginNameFromArgs(args, out value))
                return value;

            string[] names =
            {
                "hitMargin",
                "margin",
                "hitMarginType",
                "marginType",
                "judgement",
                "judgment",
                "judgementType",
                "judgmentType"
            };

            object memberValue;
            for (int i = 0; i < names.Length; i++)
            {
                if (TryGetMemberValue(hitText, names[i], out memberValue) && memberValue != null)
                {
                    value = memberValue.ToString();
                    if (LooksLikeMarginName(value))
                        return value;
                }
            }

            List<object> textObjects = GetHitTextObjects(hitText);
            for (int i = 0; i < textObjects.Count; i++)
            {
                value = GetText(textObjects[i]);
                if (LooksLikeMarginName(value))
                    return value;
            }

            return "";
        }

        public static object GetHitTextObject(object hitText)
        {
            List<object> textObjects = GetHitTextObjects(hitText);
            return textObjects.Count == 0 ? null : textObjects[0];
        }

        public static List<object> GetHitTextObjects(object hitText)
        {
            List<object> result = new List<object>();
            if (hitText == null)
                return result;

            object value;
            string[] names =
            {
                "text",
                "textMesh",
                "txt",
                "hitText",
                "textComponent",
                "textMeshPro",
                "tmpText",
                "judgementText",
                "judgmentText"
            };

            for (int i = 0; i < names.Length; i++)
            {
                if (TryGetMemberValue(hitText, names[i], out value))
                    TryAddTextObject(result, value);
            }

            TryAddTextObject(result, hitText);

            // 精准字段版默认不再扫描子物体。
            // 旧兜底函数只保留命名字段读取，避免 GetComponentsInChildren 带来额外开销。

            return result;
        }

        public static bool ApplyHitTextReplacement(object hitText, string replacement)
        {
            return ApplyHitTextReplacement(GetHitTextObjects(hitText), replacement);
        }

        public static bool ApplyHitTextReplacement(List<object> textObjects, string replacement)
        {
            if (textObjects == null || textObjects.Count == 0)
                return false;

            bool changed = false;
            for (int i = 0; i < textObjects.Count; i++)
            {
                SetSupportRichText(textObjects[i], true);
                if (SetText(textObjects[i], replacement, false))
                    changed = true;
            }

            return changed;
        }

        public static string DescribeTextObjects(List<object> textObjects)
        {
            if (textObjects == null || textObjects.Count == 0)
                return "-";

            string result = "";
            int limit = Math.Min(textObjects.Count, 3);
            for (int i = 0; i < limit; i++)
            {
                if (i > 0)
                    result += ", ";
                result += GetObjectName(textObjects[i]) + "/" + textObjects[i].GetType().Name;
            }
            if (textObjects.Count > limit)
                result += " ...";
            return result;
        }

        public static bool TryGetHitTextColor(object hitText, object textObject, string marginName, object[] args, out Color color, out string source)
        {
            object value;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] is Color)
                    {
                        color = (Color)args[i];
                        source = "Show arg Color #" + i;
                        return true;
                    }

                    if (args[i] is Color32)
                    {
                        color = (Color)(Color32)args[i];
                        source = "Show arg Color32 #" + i;
                        return true;
                    }
                }
            }

            string[] names = { "color", "textColor", "judgeColor", "judgementColor", "judgmentColor", "hitColor", "marginColor" };
            for (int i = 0; i < names.Length; i++)
            {
                if (TryGetMemberValue(hitText, names[i], out value) && TryConvertToColor(value, out color))
                {
                    source = "hitText." + names[i];
                    return true;
                }
            }

            if (textObject != null)
            {
                for (int i = 0; i < names.Length; i++)
                {
                    if (TryGetMemberValue(textObject, names[i], out value) && TryConvertToColor(value, out color))
                    {
                        source = "mainText." + names[i];
                        return true;
                    }
                }
            }

            List<object> textObjects = GetHitTextObjects(hitText);
            for (int i = 0; i < textObjects.Count; i++)
            {
                for (int n = 0; n < names.Length; n++)
                {
                    if (TryGetMemberValue(textObjects[i], names[n], out value) && TryConvertToColor(value, out color))
                    {
                        source = "childText." + names[n];
                        return true;
                    }
                }
            }

            if (TryGetDefaultMarginColor(marginName, out color))
            {
                source = "default margin color";
                return true;
            }

            source = "not found";
            return false;
        }

        public static bool TryGetHitTextColor(object hitText, object textObject, string marginName, object[] args, out Color color)
        {
            string source;
            return TryGetHitTextColor(hitText, textObject, marginName, args, out color, out source);
        }

        public static bool ShouldReplaceMargin(string marginName, Settings settings)
        {
            if (settings == null || string.IsNullOrEmpty(marginName))
                return false;

            switch (CanonicalMarginName(marginName))
            {
                case "tooearly": return settings.ReplaceTooEarly;
                case "veryearly": return settings.ReplaceVeryEarly;
                case "earlyperfect": return settings.ReplaceEarlyPerfect;
                case "perfect":
                case "pureperfect": return settings.ReplacePerfect;
                case "lateperfect": return settings.ReplaceLatePerfect;
                case "verylate": return settings.ReplaceVeryLate;
                case "toolate": return settings.ReplaceTooLate;
                case "multipress": return settings.ReplaceMultipress;
                case "failmiss":
                case "miss": return settings.ReplaceFailMiss;
                case "failoverload":
                case "overload": return settings.ReplaceFailOverload;
                default: return false;
            }
        }

        private static bool TryGetMarginNameFromArgs(object[] args, out string marginName)
        {
            marginName = "";
            if (args == null)
                return false;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == null)
                    continue;

                Type type = args[i].GetType();
                string value = args[i].ToString();

                if (type.IsEnum || type.Name.IndexOf("Margin", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    type.Name.IndexOf("Judg", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    args[i] is string)
                {
                    if (LooksLikeMarginName(value))
                    {
                        marginName = value;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool LooksLikeMarginName(string value)
        {
            return !string.IsNullOrEmpty(CanonicalMarginName(value));
        }

        private static string CanonicalMarginName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            string raw = StripRichTextTags(value).ToLowerInvariant();

            if ((raw.Contains("early") || raw.Contains("早")) && (raw.Contains("perfect") || raw.Contains("完美") || raw.Contains("완벽")))
                return "earlyperfect";
            if ((raw.Contains("late") || raw.Contains("晚")) && (raw.Contains("perfect") || raw.Contains("完美") || raw.Contains("완벽")))
                return "lateperfect";

            string normalized = NormalizeMarginName(value);

            if (normalized.Contains("tooearly") || normalized.Contains("veryearlybad"))
                return "tooearly";
            if (normalized.Contains("veryearly"))
                return "veryearly";
            if (normalized.Contains("earlyperfect") || normalized.Contains("eperfect"))
                return "earlyperfect";
            if (normalized.Contains("pureperfect"))
                return "pureperfect";
            if (normalized.Contains("perfect") || raw.Contains("完美") || raw.Contains("완벽"))
                return "perfect";
            if (normalized.Contains("lateperfect") || normalized.Contains("lperfect"))
                return "lateperfect";
            if (normalized.Contains("verylate"))
                return "verylate";
            if (normalized.Contains("toolate"))
                return "toolate";
            if (normalized.Contains("multipress"))
                return "multipress";
            if (normalized.Contains("failmiss"))
                return "failmiss";
            if (normalized.Contains("miss"))
                return "miss";
            if (normalized.Contains("failoverload"))
                return "failoverload";
            if (normalized.Contains("overload"))
                return "overload";

            if ((raw.Contains("too") && raw.Contains("early")) || raw.Contains("太早") || raw.Contains("过早"))
                return "tooearly";
            if ((raw.Contains("too") && raw.Contains("late")) || raw.Contains("太晚") || raw.Contains("过晚"))
                return "toolate";

            return "";
        }

        private static string NormalizeMarginName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            string result = StripRichTextTags(value).ToLowerInvariant();
            result = result.Replace("hitmargin", "").Replace("margin", "");
            result = result.Replace("judgement", "").Replace("judgment", "");
            result = result.Replace(" ", "").Replace("_", "").Replace("-", "");
            result = result.Replace("!", "").Replace(".", "").Replace(":", "").Replace("<", "").Replace(">", "");
            result = result.Replace("color=", "").Replace("/color", "");
            return result;
        }

        private static string StripRichTextTags(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            char[] buffer = new char[value.Length];
            int length = 0;
            bool insideTag = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '<')
                {
                    insideTag = true;
                    continue;
                }

                if (c == '>')
                {
                    insideTag = false;
                    continue;
                }

                if (!insideTag)
                    buffer[length++] = c;
            }

            return new string(buffer, 0, length);
        }

        private static bool TryGetDefaultMarginColor(string marginName, out Color color)
        {
            switch (CanonicalMarginName(marginName))
            {
                case "perfect":
                case "pureperfect":
                    color = new Color(1f, 0.9f, 0.35f);
                    return true;
                case "earlyperfect":
                case "lateperfect":
                    color = new Color(0.55f, 0.85f, 1f);
                    return true;
                case "veryearly":
                    color = new Color(1f, 0.55f, 0.1f);
                    return true;
                case "verylate":
                    color = new Color(0.65f, 0.55f, 1f);
                    return true;
                case "tooearly":
                case "toolate":
                case "multipress":
                case "failmiss":
                case "miss":
                case "failoverload":
                case "overload":
                    color = new Color(1f, 0.25f, 0.25f);
                    return true;
                default:
                    color = Color.white;
                    return false;
            }
        }

        private static void TryAddTextObject(List<object> list, object textObject)
        {
            if (textObject == null || textObject is string)
                return;

            if (FindMember(textObject.GetType(), "text") == null)
                return;

            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], textObject))
                    return;
            }

            list.Add(textObject);
        }

        private static bool TryConvertToColor(object value, out Color color)
        {
            if (value is Color)
            {
                color = (Color)value;
                return true;
            }

            if (value is Color32)
            {
                color = (Color)(Color32)value;
                return true;
            }

            color = Color.white;
            return false;
        }

        private static GameObject GetGameObject(object target)
        {
            if (target == null)
                return null;

            if (target is GameObject)
                return (GameObject)target;

            Component component = target as Component;
            if (component != null)
                return component.gameObject;

            object value;
            if (TryGetMemberValue(target, "gameObject", out value) && value is GameObject)
                return (GameObject)value;

            return null;
        }

        public static bool IsActiveHitTextObject(object hitText)
        {
            GameObject gameObject = GetGameObject(hitText);
            if (gameObject == null)
                return true;

            return gameObject.activeInHierarchy || gameObject.activeSelf;
        }

        public static bool SetText(object textObject, string value, bool append)
        {
            if (textObject == null)
                return false;

            MemberInfo member = FindMember(textObject.GetType(), "text");
            if (member == null)
                return false;

            string nextValue = value ?? "";
            if (append)
            {
                object oldValue = ReadMember(textObject, member);
                nextValue = (oldValue == null ? "" : oldValue.ToString()) + nextValue;
            }

            return WriteMember(textObject, member, nextValue);
        }

        public static string GetText(object textObject)
        {
            if (textObject == null)
                return "";

            MemberInfo member = FindMember(textObject.GetType(), "text");
            if (member == null)
                return "";

            object value = ReadMember(textObject, member);
            return value == null ? "" : value.ToString();
        }

        public static bool AppendToResultText(object target, string info)
        {
            if (target == null || string.IsNullOrEmpty(info))
                return false;

            string[] memberNames =
            {
                "txtResults",
                "txtResult",
                "resultText",
                "resultsText",
                "txtStats",
                "statsText",
                "txtWin",
                "txtWinResults",
                "txtLevelComplete",
                "txtLevelCleared",
                "txtClear",
                "txtComplete",
                "clearText",
                "completeText"
            };

            object textObject;
            for (int i = 0; i < memberNames.Length; i++)
            {
                if (TryGetMemberValue(target, memberNames[i], out textObject) && TryAppendToTextObject(textObject, info, false))
                    return true;
            }

            object gameObject;
            if (TryGetMemberValue(target, "gameObject", out gameObject) && TryAppendToGameObjectTexts(gameObject as GameObject, info))
                return true;

            return FindAndAppendToVisibleResultText(info);
        }

        private static bool TryAppendToGameObjectTexts(GameObject gameObject, string info)
        {
            if (gameObject == null)
                return false;

            Component[] components = gameObject.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (FindMember(components[i].GetType(), "text") != null &&
                    IsLikelyResultText(components[i].name, GetText(components[i])) &&
                    TryAppendToTextObject(components[i], info, true))
                    return true;
            }

            return false;
        }

        private static bool FindAndAppendToVisibleResultText(string info)
        {
            TextMesh[] meshes = UnityEngine.Object.FindObjectsOfType<TextMesh>();
            for (int i = 0; i < meshes.Length; i++)
            {
                if (IsActiveTextObject(meshes[i]) && IsLikelyResultText(meshes[i].name, meshes[i].text) &&
                    TryAppendToTextObject(meshes[i], info, true))
                    return true;
            }

            UnityEngine.UI.Text[] texts = UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Text>();
            for (int i = 0; i < texts.Length; i++)
            {
                if (IsActiveTextObject(texts[i]) && IsLikelyResultText(texts[i].name, texts[i].text) &&
                    TryAppendToTextObject(texts[i], info, true))
                    return true;
            }

            Component[] components = UnityEngine.Object.FindObjectsOfType<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null || components[i] is TextMesh || components[i] is UnityEngine.UI.Text)
                    continue;

                if (FindMember(components[i].GetType(), "text") != null &&
                    IsActiveTextObject(components[i]) &&
                    IsLikelyResultText(components[i].name, GetText(components[i])) &&
                    TryAppendToTextObject(components[i], info, true))
                    return true;
            }

            return false;
        }

        private static bool TryAppendToTextObject(object textObject, string info, bool requireResultCandidate)
        {
            if (textObject == null || !IsActiveTextObject(textObject))
                return false;

            string oldText = GetText(textObject);
            if (requireResultCandidate && !IsLikelyResultText(GetObjectName(textObject), oldText))
                return false;

            if (ContainsAverageTimingMarker(oldText))
                return true;

            SetSupportRichText(textObject, true);
            return SetText(textObject, info, true);
        }

        private static bool ContainsAverageTimingMarker(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            return text.Contains(Locale_zh.Avg_Timing) || text.Contains(Locale_en.Avg_Timing);
        }

        private static string GetObjectName(object textObject)
        {
            object value;
            if (TryGetMemberValue(textObject, "name", out value) && value != null)
                return value.ToString();

            object gameObject;
            if (TryGetMemberValue(textObject, "gameObject", out gameObject) &&
                TryGetMemberValue(gameObject, "name", out value) && value != null)
                return value.ToString();

            return "";
        }

        private static bool IsLikelyResultText(string objectName, string text)
        {
            string combined = ((objectName ?? "") + " " + (text ?? "")).ToLowerInvariant();
            if (combined.Length == 0)
                return false;

            string[] hints =
            {
                "result",
                "results",
                "stat",
                "clear",
                "complete",
                "win",
                "accuracy",
                "perfect",
                "miss",
                "score",
                "rank",
                "결과",
                "정확도",
                "완벽",
                "结算",
                "结果",
                "准确",
                "完美"
            };

            for (int i = 0; i < hints.Length; i++)
            {
                if (combined.Contains(hints[i]))
                    return true;
            }

            return false;
        }

        public static void SetSupportRichText(object textObject, bool value)
        {
            if (textObject == null)
                return;

            MemberInfo member = FindMember(textObject.GetType(), "supportRichText");
            if (member != null)
                WriteMember(textObject, member, value);

            member = FindMember(textObject.GetType(), "richText");
            if (member != null)
                WriteMember(textObject, member, value);
        }

        public static bool IsActiveTextObject(object textObject)
        {
            if (textObject == null)
                return false;

            GameObject gameObject = GetGameObject(textObject);
            if (gameObject != null)
                return gameObject.activeInHierarchy || gameObject.activeSelf;

            return true;
        }

        public static bool TryGetMemberValue(object targetOrType, string name, out object value)
        {
            value = null;
            if (targetOrType == null || string.IsNullOrEmpty(name))
                return false;

            Type type = targetOrType as Type;
            object target = type == null ? targetOrType : null;
            if (type == null)
                type = targetOrType.GetType();

            MemberInfo member = FindMember(type, name);
            if (member == null)
                return false;

            try
            {
                value = ReadMember(target, member);
                return true;
            }
            catch
            {
                value = null;
                return false;
            }
        }

        public static bool TryGetPath(object root, string path, out object value)
        {
            value = root;
            if (root == null || string.IsNullOrEmpty(path))
                return false;

            string[] parts = path.Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                if (!TryGetMemberValue(value, parts[i], out value) || value == null)
                    return false;
            }

            return true;
        }

        private static MemberInfo FindMember(Type type, string name)
        {
            if (type == null)
                return null;

            string key = type.FullName + "::" + name;
            MemberInfo cached;
            if (MemberCache.TryGetValue(key, out cached))
                return cached;

            Type current = type;
            while (current != null)
            {
                FieldInfo field = current.GetField(name, Flags);
                if (field != null)
                {
                    MemberCache[key] = field;
                    return field;
                }

                PropertyInfo prop = current.GetProperty(name, Flags);
                if (prop != null)
                {
                    MemberCache[key] = prop;
                    return prop;
                }

                current = current.BaseType;
            }

            MemberCache[key] = null;
            return null;
        }

        private static object ReadMember(object target, MemberInfo member)
        {
            FieldInfo field = member as FieldInfo;
            if (field != null)
                return field.GetValue(target);

            PropertyInfo prop = member as PropertyInfo;
            if (prop != null)
                return prop.GetValue(target, null);

            return null;
        }

        private static bool WriteMember(object target, MemberInfo member, object value)
        {
            try
            {
                FieldInfo field = member as FieldInfo;
                if (field != null)
                {
                    field.SetValue(target, value);
                    return true;
                }

                PropertyInfo prop = member as PropertyInfo;
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(target, value, null);
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static double ToDouble(object value, double fallback)
        {
            if (value == null)
                return fallback;

            try
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static int ToInt(object value, int fallback)
        {
            if (value == null)
                return fallback;

            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static bool ToBool(object value, bool fallback)
        {
            if (value == null)
                return fallback;

            try
            {
                return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }
    }
}
