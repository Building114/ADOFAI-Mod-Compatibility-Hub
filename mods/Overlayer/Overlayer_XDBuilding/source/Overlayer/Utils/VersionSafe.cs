using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Overlayer.Utils;

public static class VersionSafe
{
    private const BindingFlags Any =
        BindingFlags.Public |
        BindingFlags.NonPublic |
        BindingFlags.Instance |
        BindingFlags.Static |
        BindingFlags.FlattenHierarchy;

    public static readonly int ReleaseNumber = ResolveReleaseNumber();

    private static int ResolveReleaseNumber()
    {
        object value =
            GetStaticMemberValue("VersionControl", "releaseNumber") ??
            GetStaticMemberValue("GCNS", "releaseNumber") ??
            GetStaticMemberValue("ADOBase", "releaseNumber");

        if (TryToInt(value, out int number))
        {
            return number;
        }

                                                                                
                                                                                
                                        
        return MiscUtils.TypeByName("scrPlayerManager") != null ? 141 : 140;
    }

    public static double GetPlanetSpeed(scrController controller)
    {
        if (controller == null)
        {
            return 1.0;
        }

        object playerOne = GetPlayerOne(controller);
        object planetarySystem = FirstMemberValue(playerOne, "planetarySystem", "planetSystem");
        object newSpeed = FirstMemberValue(planetarySystem, "speed", "planetSpeed");

        if (TryToDouble(newSpeed, out double speed))
        {
            return speed;
        }

        object oldSpeed = FirstMemberValue(controller, "speed", "planetSpeed");

        return TryToDouble(oldSpeed, out speed) ? speed : 1.0;
    }

    public static object GetPlayerOne(scrController controller = null)
    {
        controller ??= scrController.instance;

        return FirstMemberValue(controller, "playerOne", "player") ??
               GetPlayerByIndex(0);
    }

    public static object GetPlayerByIndex(int index)
    {
        if (index < 0)
        {
            return null;
        }

        object playerManager = GetStaticMemberValue("scrPlayerManager", "instance");
        object players = FirstMemberValue(playerManager, "players", "listPlayers", "playerList");

        if (players is IList list)
        {
            return index < list.Count ? list[index] : null;
        }

        if (players is IEnumerable items)
        {
            int current = 0;
            foreach (object player in items)
            {
                if (current++ == index)
                {
                    return player;
                }
            }
        }

        return null;
    }

    public static bool IsClockwise(scrController controller)
    {
        if (controller == null)
        {
            return true;
        }

        object playerOne = GetPlayerOne(controller);
        object value =
            FirstMemberValue(playerOne, "isCW", "clockwise") ??
            FirstMemberValue(controller, "isCW", "clockwise");

        return value is bool boolValue ? boolValue : true;
    }

    public static bool IsMidspinInfiniteMargin(scrController controller)
    {
        if (controller == null)
        {
            return false;
        }

        object value =
            FirstMemberValue(GetPlayerOne(controller), "midspinInfiniteMargin") ??
            FirstMemberValue(controller, "midspinInfiniteMargin");

        return value is bool boolValue && boolValue;
    }

    public static int GetConsecMultipressCounter(scrController controller)
    {
        return GetConsecMultipressCounter((object)controller);
    }

    public static int GetConsecMultipressCounter(object source)
    {
        if (source == null)
        {
            return 0;
        }

        object value =
            FirstMemberValue(source, "consecMultipressCounter") ??
            FirstMemberValue(FirstMemberValue(source, "playerOne", "player"), "consecMultipressCounter");

        return TryToInt(value, out int result) ? result : 0;
    }

    public static float GetFailCounter(scrController controller, string counterName)
    {
        if (controller == null || string.IsNullOrWhiteSpace(counterName))
        {
            return float.NaN;
        }

        object playerOne = GetPlayerOne(controller);
        object failbar =
            FirstMemberValue(playerOne, "failbar", "failBar") ??
            FirstMemberValue(controller, "failbar", "failBar");

        object value = FirstMemberValue(failbar, counterName) ?? FirstMemberValue(playerOne, counterName) ?? FirstMemberValue(controller, counterName);

        return TryToFloat(value, out float result) ? result : float.NaN;
    }

    public static scrMistakesManager GetMistakesManager(scrController controller = null)
    {
        object playerManager = GetStaticMemberValue("scrPlayerManager", "instance");
        object newManager = FirstMemberValue(playerManager, "mistakesManager");

        if (newManager is scrMistakesManager newer)
        {
            return newer;
        }

        object oldManager = FirstMemberValue(controller ?? scrController.instance, "mistakesManager");

        return oldManager as scrMistakesManager;
    }

    public static void CalculatePercentAcc()
    {
        object trackersObject =
            GetStaticMemberValue("scrMistakesManager", "marginTrackers") ??
            FirstMemberValue(GetMistakesManager(), "marginTrackers");

        bool invokedAny = false;
        object manager = GetMistakesManager();
        if (trackersObject is IEnumerable trackers)
        {
            foreach (object tracker in trackers)
            {
                bool invoked = TryInvokeCalculatePercentAcc(tracker, tracker);
                if (!invoked)
                {
                    invoked = TryInvokeCalculatePercentAcc(manager, tracker);
                }

                invokedAny |= invoked;
            }
        }

        if (!invokedAny)
        {
            TryInvokeCalculatePercentAcc(manager, null);
        }
    }

    private static bool TryInvokeCalculatePercentAcc(object target, object tracker)
    {
        if (target == null)
        {
            return false;
        }

        Type type = target as Type ?? target.GetType();
        object instance = target is Type ? null : target;

        foreach (MethodInfo method in type.GetMethods(Any))
        {
            if (!string.Equals(method.Name, "CalculatePercentAcc", StringComparison.Ordinal))
            {
                continue;
            }

            ParameterInfo[] parameters = method.GetParameters();
            object[] arguments;

            if (parameters.Length == 0)
            {
                arguments = null;
            }
            else if (parameters.Length == 1 && tracker != null &&
                     parameters[0].ParameterType.IsInstanceOfType(tracker))
            {
                arguments = new[] { tracker };
            }
            else if (parameters.Length > 0 && Array.TrueForAll(parameters, parameter => parameter.IsOptional))
            {
                arguments = Array.ConvertAll(parameters, parameter => parameter.DefaultValue);
            }
            else
            {
                continue;
            }

            try
            {
                method.Invoke(method.IsStatic ? null : instance, arguments);
                return true;
            }
            catch
            {
            }
        }

        return false;
    }

    public static float GetPercentAcc()
    {
        object value = FirstMemberValue(GetMistakesManager(), "percentAcc");
        return TryToFloat(value, out float result) ? result : GetPercentAcc(0);
    }

    public static float GetPercentAcc(int playerIndex)
    {
        object value = FirstMemberValue(GetMarginTracker(playerIndex), "percentAcc");
        if (TryToFloat(value, out float result))
        {
            return result;
        }

        if (playerIndex != 0 || GetMarginTracker(playerIndex) != null)
        {
            return float.NaN;
        }

        object playerManager = GetStaticMemberValue("scrPlayerManager", "instance");
        object newManager = FirstMemberValue(playerManager, "mistakesManager");
        value = FirstMemberValue(newManager, "percentAcc") ?? FirstMemberValue(GetMistakesManager(), "percentAcc");

        return TryToFloat(value, out result) ? result : float.NaN;
    }

    public static float GetPercentXAcc()
    {
        object value = FirstMemberValue(GetMistakesManager(), "percentXAcc");
        return TryToFloat(value, out float result) ? result : GetPercentXAcc(0);
    }

    public static float GetPercentXAcc(int playerIndex)
    {
        object value = FirstMemberValue(GetMarginTracker(playerIndex), "percentXAcc");
        if (TryToFloat(value, out float result))
        {
            return result;
        }

        if (playerIndex != 0 || GetMarginTracker(playerIndex) != null)
        {
            return float.NaN;
        }

        object playerManager = GetStaticMemberValue("scrPlayerManager", "instance");
        object newManager = FirstMemberValue(playerManager, "mistakesManager");
        value = FirstMemberValue(newManager, "percentXAcc") ?? FirstMemberValue(GetMistakesManager(), "percentXAcc");

        return TryToFloat(value, out result) ? result : float.NaN;
    }

    public static float GetMaxPossibleXAcc(int playerIndex = 0)
    {
        object value = FirstMemberValue(GetMarginTracker(playerIndex), "maxPossibleXAcc");
        return TryToFloat(value, out float result) ? result : float.NaN;
    }

    public static int[] GetHitMarginsCount()
    {
        return GetHitMarginsCount(0);
    }

    public static int[] GetHitMarginsCount(int playerIndex)
    {
        object counts = FirstMemberValue(GetMarginTracker(playerIndex), "hitMarginsCount");
        if (counts is int[] result)
        {
            return result;
        }

        if (counts is IList list)
        {
            int[] values = new int[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                values[i] = TryToInt(list[i], out int count) ? count : 0;
            }

            return values;
        }

        if (playerIndex == 0 && GetMarginTracker(playerIndex) == null)
        {
            object oldCounts = GetStaticMemberValue("scrMistakesManager", "hitMarginsCount");
            return oldCounts as int[] ?? Array.Empty<int>();
        }

        return Array.Empty<int>();
    }

    public static object GetNativeTrackerValue(int playerIndex, string memberName) {
        return FirstMemberValue(GetMarginTracker(playerIndex), memberName);
    }

    public static int GetNativeTrackerInt(int playerIndex, string memberName) {
        return TryToInt(GetNativeTrackerValue(playerIndex, memberName), out int result) ? result : -1;
    }

    public static string GetNativeLastMargin(int playerIndex, bool checkpoint = false) {
        object value = GetNativeTrackerValue(playerIndex, checkpoint ? "lastCheckpointHitMargin" : "lastAddedHitMargin");
        return value?.ToString() ?? string.Empty;
    }

    public static float GetNativePercentComplete(int playerIndex) {
        object value = GetNativeTrackerValue(playerIndex, "percentComplete");
        return TryToFloat(value, out float result) ? result : float.NaN;
    }

    public static bool GetNativeIsAllPurePerfect(int playerIndex) {
        object tracker = GetMarginTracker(playerIndex);
        if(tracker == null) return false;
        try {
            MethodInfo method = tracker.GetType().GetMethod("IsAllPurePerfect", Any, null, Type.EmptyTypes, null);
            return method != null && method.Invoke(tracker, null) is bool result && result;
        } catch {
            return false;
        }
    }

    public static int GetNativeRecordedMarginsTotal(int playerIndex) {
        object hitMargins = GetNativeTrackerValue(playerIndex, "hitMargins");
        return hitMargins is ICollection collection ? collection.Count : -1;
    }

    public static int GetHitMarginsCountAt(int index)
    {
        return GetHitMarginsCountAt(index, 0);
    }

    public static int GetHitMarginsCountAt(int index, int playerIndex)
    {
        int[] counts = GetHitMarginsCount(playerIndex);

        return index >= 0 && index < counts.Length ? counts[index] : 0;
    }

    public static int GetHitMarginsTotal()
    {
        return GetHitMarginsTotal(0);
    }

    public static int GetHitMarginsTotal(int playerIndex)
    {
        int nativeTotal = GetNativeTrackerInt(playerIndex, "playerHitMarginCount");
        if (nativeTotal >= 0)
        {
            return Math.Max(0, nativeTotal
                - GetHitCount(HitMargin.FailMiss, playerIndex)
                - GetHitCount(HitMargin.FailOverload, playerIndex)
                - GetHitCount(HitMargin.FailedFloor, playerIndex));
        }

        int total = 0;
        foreach (HitMargin margin in HitMarginHelper.PlayerHitMarginTypes)
        {
            if (margin == HitMargin.FailMiss ||
                margin == HitMargin.FailOverload ||
                margin == HitMargin.FailedFloor)
            {
                continue;
            }

            total += Math.Max(0, GetHitCount(margin, playerIndex));
        }

        return total;
    }

    public static int GetHitCount(HitMargin margin)
    {
        return GetHitCount(margin, 0);
    }

    public static int GetHitCount(string marginName, int playerIndex = 0)
    {
        if (string.IsNullOrWhiteSpace(marginName))
        {
            return 0;
        }

        try
        {
            object parsed = Enum.Parse(typeof(HitMargin), marginName, true);
            if (!Enum.IsDefined(typeof(HitMargin), parsed))
            {
                return 0;
            }

            return GetHitCount((HitMargin)parsed, playerIndex);
        }
        catch
        {
            return 0;
        }
    }

    public static int GetHitCount(HitMargin margin, int playerIndex)
    {
        object tracker = GetMarginTracker(playerIndex);
        int trackerCount = InvokeGetHits(tracker, margin);
        if (trackerCount >= 0)
        {
            return trackerCount;
        }

        if (playerIndex == 0 && tracker == null)
        {
            int managerCount = InvokeGetHits(GetMistakesManager(), margin);
            if (managerCount >= 0)
            {
                return managerCount;
            }
        }

        return GetHitMarginsCountAt((int)margin, playerIndex);
    }

    private static object GetMarginTracker(int playerIndex)
    {
        if (playerIndex < 0)
        {
            return null;
        }

        object trackersObject =
            GetStaticMemberValue("scrMistakesManager", "marginTrackers") ??
            FirstMemberValue(GetMistakesManager(), "marginTrackers");

        if (trackersObject is IList list)
        {
            return playerIndex < list.Count ? list[playerIndex] : null;
        }

        if (trackersObject is IEnumerable trackers)
        {
            int index = 0;
            foreach (object tracker in trackers)
            {
                if (index++ == playerIndex)
                {
                    return tracker;
                }
            }
        }

        return null;
    }

    private static int InvokeGetHits(object target, HitMargin margin)
    {
        if (target == null)
        {
            return -1;
        }

        MethodInfo method = target.GetType().GetMethod(
            "GetHits",
            Any,
            null,
            new[] { typeof(HitMargin) },
            null
        );

        if (method == null)
        {
            return -1;
        }

        try
        {
            object result = method.Invoke(target, new object[] { margin });

            return result is int count ? count : -1;
        }
        catch
        {
            return -1;
        }
    }

    public static int GetPlayerCount()
    {
                                                             
                             
        object inst = GetStaticMemberValue("scrPlayerManager", "instance");
        object instValue = FirstMemberValue(inst, "playerCount");
        if (TryToInt(instValue, out int instCount) && instCount > 0)
        {
            return instCount;
        }

        object staticValue = GetStaticMemberValue("scrPlayerManager", "playerCount");
        if (TryToInt(staticValue, out int staticCount) && staticCount > 0)
        {
            return staticCount;
        }

        object players = FirstMemberValue(inst, "players", "listPlayers", "playerList");
        if (players is ICollection collection && collection.Count > 0)
        {
            return collection.Count;
        }

        if (players is IEnumerable items)
        {
            int count = 0;
            foreach (object player in items)
            {
                count++;
            }

            if (count > 0)
            {
                return count;
            }
        }

        return 1;
    }

    public static int GetMaxPlayerCount()
    {
        return ReleaseNumber >= 141 ? 4 : 1;
    }

    public static bool IsCoopMode()
    {
                                                         
        object inst = GetStaticMemberValue("scrPlayerManager", "instance");
        object instValue = FirstMemberValue(inst, "playerCount");
        if (TryToInt(instValue, out int instCount))
        {
            return instCount > 1;
        }

        object staticValue = GetStaticMemberValue("scrPlayerManager", "playerCount");
        if (TryToInt(staticValue, out int staticCount))
        {
            return staticCount > 1;
        }

        object coopValue = GetStaticMemberValue("scrController", "coopMode");
        return coopValue is bool boolValue && boolValue;
    }

    public static int GetCurrentSeqID(scrController controller = null)
    {
        controller ??= scrController.instance;
        object value = FirstMemberValue(controller, "currentSeqID", "seqID") ??
                       FirstMemberValue(GetPlayerOne(controller), "currentSeqID", "seqID");

        return TryToInt(value, out int result) ? result : 0;
    }

    public static float GetPercentComplete(scrController controller = null)
    {
        controller ??= scrController.instance;
        object value = FirstMemberValue(GetMistakesManager(controller), "percentComplete") ??
                       GetNativeTrackerValue(0, "percentComplete") ??
                       FirstMemberValue(controller, "percentComplete") ??
                       FirstMemberValue(GetPlayerOne(controller), "percentComplete");

        return TryToFloat(value, out float result) ? result : 0f;
    }

    public static scrFloor GetCurrentFloor(scrController controller = null)
    {
        controller ??= scrController.instance;
        object value = FirstMemberValue(controller, "currFloor", "currentFloor") ?? FirstMemberValue(GetPlayerOne(controller), "currFloor", "currentFloor");

        return value as scrFloor;
    }

    public static bool IsGameWorld(scrController controller = null)
    {
        controller ??= scrController.instance;
        object value = FirstMemberValue(controller, "gameworld", "gameWorld");

        if (value is bool boolValue)
        {
            return boolValue;
        }

        object conductorValue = FirstMemberValue(scrConductor.instance, "isGameWorld");
        return conductorValue is bool conductorBool && conductorBool;
    }

    public static bool IsPaused(scrController controller = null)
    {
        controller ??= scrController.instance;
        object value = FirstMemberValue(controller, "paused");

        return value is bool boolValue && boolValue;
    }

    public static bool IsNoFail(scrController controller = null)
    {
        controller ??= scrController.instance;
        object value = FirstMemberValue(controller, "noFail") ?? GetStaticMemberValue("GCS", "useNoFail");

        return value is bool boolValue && boolValue;
    }

    public static int GetCheckpointsUsed()
    {
        object value = GetStaticMemberValue("scrController", "checkpointsUsed");

        return TryToInt(value, out int result) ? result : 0;
    }

    public static int GetDeaths()
    {
        object value = GetStaticMemberValue("scrController", "deaths");

        return TryToInt(value, out int result) ? result : 0;
    }

    public static int GetCurrentWorld()
    {
        object value = GetStaticMemberValue("scrController", "currentWorld");

        return TryToInt(value, out int result) ? result : 0;
    }

    public static bool IsFreeroamFloor(scrFloor floor)
    {
        object value = FirstMemberValue(floor, "freeroam", "freeRoam");

        return value is bool boolValue && boolValue;
    }

    public static bool IsSafeFloor(scrFloor floor)
    {
        object value = FirstMemberValue(floor, "isSafe", "safe");

        return value is bool boolValue && boolValue;
    }

    public static bool IsMidSpinFloor(scrFloor floor)
    {
        object value = FirstMemberValue(floor, "midSpin", "midspin");

        return value is bool boolValue && boolValue;
    }

    public static double GetMarginScale(scrFloor floor = null)
    {
        floor ??= GetCurrentFloor();
        object value = FirstMemberValue(floor, "marginScale");

        return TryToDouble(value, out double result) ? result : 0;
    }

    public static IList GetLevelFloors()
    {
        object lm = FirstMemberValue(MiscUtils.TypeByName("ADOBase"), "lm");
        object floors = FirstMemberValue(lm, "listFloors", "floors") ??
                       FirstMemberValue(scrLevelMaker.instance, "listFloors", "floors");

        return floors as IList;
    }

    public static double GetFloorEntryTime(object floor)
    {
        object value = FirstMemberValue(floor, "entryTime", "floorEntryTime");
        return TryToDouble(value, out double result) ? result : double.NaN;
    }

    public static void LoadScene(string name)
    {
        object loader = GetStaticMemberValue("ADOBase", "loader");
        if (Invoke(loader, "LoadScene", name))
        {
            return;
        }

        Invoke(MiscUtils.TypeByName("ADOBase"), "LoadScene", name);
    }

    public static object GetStaticMemberValue(string typeName, string memberName)
    {
        Type type = MiscUtils.TypeByName(typeName);

        return type == null ? null : GetMemberValue(type, memberName);
    }

    public static object GetMemberValue(object target, string memberName)
    {
        if (target == null || string.IsNullOrWhiteSpace(memberName))
        {
            return null;
        }

        Type type = target as Type ?? target.GetType();
        object instance = target is Type ? null : target;

        try
        {
            FieldInfo field = type.GetField(memberName, Any);
            if (field != null)
            {
                return field.GetValue(instance);
            }

            PropertyInfo property = type.GetProperty(memberName, Any);
            if (property != null && property.GetIndexParameters().Length == 0)
            {
                return property.GetValue(instance);
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    public static object FirstMemberValue(object target, params string[] memberNames)
    {
        if (target == null || memberNames == null)
        {
            return null;
        }

        foreach (string memberName in memberNames)
        {
            object value = GetMemberValue(target, memberName);
            if (value != null)
            {
                return value;
            }
        }

        return null;
    }

    private static void InvokeNoArg(object target, string methodName)
    {
        if (target == null || string.IsNullOrWhiteSpace(methodName))
        {
            return;
        }

        try
        {
            Type type = target as Type ?? target.GetType();
            object instance = target is Type ? null : target;
            type.GetMethod(methodName, Any, null, Type.EmptyTypes, null)?.Invoke(instance, null);
        }
        catch
        {
                                
        }
    }

    private static bool Invoke(object target, string methodName, params object[] args)
    {
        if (target == null || string.IsNullOrWhiteSpace(methodName))
        {
            return false;
        }

        try
        {
            Type type = target as Type ?? target.GetType();
            object instance = target is Type ? null : target;
            foreach (MethodInfo method in type.GetMethods(Any))
            {
                if (method.Name != methodName)
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != args.Length)
                {
                    continue;
                }

                method.Invoke(instance, args);
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool TryToDouble(object value, out double result)
    {
        try
        {
            if (value == null)
            {
                result = 0;
                return false;
            }

            result = Convert.ToDouble(value);
            return true;
        }
        catch
        {
            result = 0;
            return false;
        }
    }

    private static bool TryToFloat(object value, out float result)
    {
        try
        {
            if (value == null)
            {
                result = 0;
                return false;
            }

            result = Convert.ToSingle(value);
            return true;
        }
        catch
        {
            result = 0;
            return false;
        }
    }

    private static bool TryToInt(object value, out int result)
    {
        try
        {
            if (value == null)
            {
                result = 0;
                return false;
            }

            result = Convert.ToInt32(value);
            return true;
        }
        catch
        {
            result = 0;
            return false;
        }
    }
}
