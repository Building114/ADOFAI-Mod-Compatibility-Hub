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
        object playerManager = GetStaticMemberValue("scrPlayerManager", "instance");
        object players = FirstMemberValue(playerManager, "players", "listPlayers", "playerList");

        if (players is IList list && index >= 0 && index < list.Count)
        {
            return list[index];
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

        if (trackersObject is IEnumerable trackers)
        {
            bool invokedAny = false;
            foreach (object tracker in trackers)
            {
                InvokeNoArg(tracker, "CalculatePercentAcc");
                invokedAny = true;
            }

            if (invokedAny)
            {
                return;
            }
        }

        InvokeNoArg(GetMistakesManager(), "CalculatePercentAcc");
    }

    public static float GetPercentAcc()
    {
        object playerManager = GetStaticMemberValue("scrPlayerManager", "instance");
        object newManager = FirstMemberValue(playerManager, "mistakesManager");
        object value = FirstMemberValue(newManager, "percentAcc") ?? FirstMemberValue(GetMistakesManager(), "percentAcc");

        return TryToFloat(value, out float result) ? result : float.NaN;
    }

    public static float GetPercentXAcc()
    {
        object playerManager = GetStaticMemberValue("scrPlayerManager", "instance");
        object newManager = FirstMemberValue(playerManager, "mistakesManager");
        object value = FirstMemberValue(newManager, "percentXAcc") ?? FirstMemberValue(GetMistakesManager(), "percentXAcc");

        return TryToFloat(value, out float result) ? result : float.NaN;
    }

    public static int[] GetHitMarginsCount()
    {
        return GetHitMarginsCount(0);
    }

    public static int[] GetHitMarginsCount(int playerIndex)
    {
        object trackersObject =
            GetStaticMemberValue("scrMistakesManager", "marginTrackers") ??
            FirstMemberValue(GetMistakesManager(), "marginTrackers");

        if (trackersObject is IList list && playerIndex >= 0 && playerIndex < list.Count)
        {
            object counts = FirstMemberValue(list[playerIndex], "hitMarginsCount");
            if (counts is int[] result)
            {
                return result;
            }
        }

        if (trackersObject is IEnumerable trackers)
        {
            foreach (object tracker in trackers)
            {
                object counts = FirstMemberValue(tracker, "hitMarginsCount");

                if (counts is int[] result)
                {
                    return result;
                }
            }
        }

        object oldCounts = GetStaticMemberValue("scrMistakesManager", "hitMarginsCount");

        return oldCounts as int[] ?? Array.Empty<int>();
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
        object tracker = GetMarginTracker(playerIndex);
        object trackerMargins = FirstMemberValue(tracker, "hitMargins");
        if (trackerMargins is ICollection trackerCollection)
        {
            return trackerCollection.Count;
        }

        object manager = GetMistakesManager();

        object newMargins = FirstMemberValue(manager, "hitMargins");
        if (newMargins is ICollection newCollection)
        {
            return newCollection.Count;
        }

        object oldMargins = GetStaticMemberValue("scrMistakesManager", "hitMargins");
        if (oldMargins is ICollection oldCollection)
        {
            return oldCollection.Count;
        }

        int total = 0;
        foreach (int count in GetHitMarginsCount(playerIndex))
        {
            total += Math.Max(0, count);
        }

        return total;
    }

    public static int GetHitCount(HitMargin margin)
    {
        return GetHitCount(margin, 0);
    }

    public static int GetHitCount(HitMargin margin, int playerIndex)
    {
        object tracker = GetMarginTracker(playerIndex);
        int trackerCount = InvokeGetHits(tracker, margin);
        if (trackerCount >= 0)
        {
            return trackerCount;
        }

        int managerCount = InvokeGetHits(GetMistakesManager(), margin);
        if (managerCount >= 0)
        {
            return managerCount;
        }

        return GetHitMarginsCountAt((int)margin, playerIndex);
    }

    private static object GetMarginTracker(int playerIndex)
    {
        object trackersObject =
            GetStaticMemberValue("scrMistakesManager", "marginTrackers") ??
            FirstMemberValue(GetMistakesManager(), "marginTrackers");

        if (trackersObject is IList list && playerIndex >= 0 && playerIndex < list.Count)
        {
            return list[playerIndex];
        }

        if (trackersObject is IEnumerable trackers)
        {
            foreach (object tracker in trackers)
            {
                return tracker;
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
        object value = FirstMemberValue(controller, "percentComplete") ??
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
