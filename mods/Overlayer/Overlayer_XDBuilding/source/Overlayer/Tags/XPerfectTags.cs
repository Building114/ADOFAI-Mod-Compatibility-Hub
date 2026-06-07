using Overlayer.Tags.Attributes;
using Overlayer.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace Overlayer.Tags;

/// <summary>
/// Minimal source-exact soft adapter for XPerfect.
///
/// Reads only the public state that exists in XPerfect source:
///   XPerfect.AccuracyState.PlusPerfectCount
///   XPerfect.AccuracyState.XPerfectCount
///   XPerfect.AccuracyState.MinusPerfectCount
///   XPerfect.AccuracyState.LastJudge
///   XPerfect.AccuracyState.LastJudgeForText
///   XPerfect.AccuracyState.LastJudgeConsumedByMeter
///
/// It does not expose player parameters because current XPerfect keeps global
/// static counters, not per-player counters. This avoids misleading tags such
/// as {XPerfectCount:2}.
///
/// It still uses reflection so Overlayer does not need a hard reference to
/// XPerfect.dll. Type/member lookups are cached.
/// </summary>
public static class XPerfectTags
{
    private const BindingFlags StaticAny =
        BindingFlags.Public |
        BindingFlags.NonPublic |
        BindingFlags.Static |
        BindingFlags.FlattenHierarchy;

    private const double MissingTypeRescanSeconds = 2.0;

    private static readonly Dictionary<string, MemberInfo> MemberCache = new(StringComparer.Ordinal);
    private static Type accuracyStateType;
    private static DateTime lastMissingTypeScanUtc = DateTime.MinValue;

    [Tag(NotPlaying = true)]
    public static bool XPerfectAvailable => TryEnsureAccuracyStateType();

    [Tag]
    public static int XPerfectCount()
    {
        return ReadInt("XPerfectCount");
    }

    [Tag]
    public static int XPlusPerfectCount()
    {
        return ReadInt("PlusPerfectCount");
    }

    [Tag]
    public static int XMinusPerfectCount()
    {
        return ReadInt("MinusPerfectCount");
    }

    [Tag]
    public static int XDetailedPerfectCount()
    {
        return XPlusPerfectCount() + XPerfectCount() + XMinusPerfectCount();
    }

    [Tag]
    public static string XPerfectDetail()
    {
        return $"+{XPlusPerfectCount()}/{XPerfectCount()}/-{XMinusPerfectCount()}";
    }

    [Tag]
    public static string XPerfectLastJudge(bool textJudge = true)
    {
        object value = ReadMember(textJudge ? "LastJudgeForText" : "LastJudge");
        return value?.ToString() ?? string.Empty;
    }

    [Tag]
    public static bool XPerfectLastJudgeConsumedByMeter()
    {
        return ReadBool("LastJudgeConsumedByMeter");
    }

    [Tag]
    public static bool XPerfectPureRun()
    {
        return XDetailedPerfectCount() > 0 &&
            XPerfectCount() > 0 &&
            XPlusPerfectCount() == 0 &&
            XMinusPerfectCount() == 0;
    }

    [Tag]
    public static double XPerfectShare(int digits = -1)
    {
        double total = XDetailedPerfectCount();
        if (total <= 0)
        {
            return 0;
        }

        return (XPerfectCount() / total * 100.0).Round(digits);
    }

    [Tag]
    public static double XPerfectPercent(int digits = -1)
    {
        // XPerfect source does not expose another percent field.
        // This is only a friendly alias of XPerfectShare.
        return XPerfectShare(digits);
    }

    [Tag("XPerfectRate")]
    public static double XPerfectRate(int digits = -1) => XPerfectPercent(digits);

    private static bool TryEnsureAccuracyStateType()
    {
        if (accuracyStateType != null)
        {
            return true;
        }

        DateTime now = DateTime.UtcNow;
        if ((now - lastMissingTypeScanUtc).TotalSeconds < MissingTypeRescanSeconds)
        {
            return false;
        }

        lastMissingTypeScanUtc = now;

        Type direct = Type.GetType("XPerfect.AccuracyState", false);
        if (direct != null)
        {
            accuracyStateType = direct;
            MemberCache.Clear();
            return true;
        }

        try
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType("XPerfect.AccuracyState", false);
                if (type == null)
                {
                    continue;
                }

                accuracyStateType = type;
                MemberCache.Clear();
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static object ReadMember(string exactName)
    {
        if (!TryEnsureAccuracyStateType())
        {
            return null;
        }

        try
        {
            MemberInfo member = GetMember(exactName);
            if (member is PropertyInfo property)
            {
                MethodInfo getter = property.GetGetMethod(true);
                if (getter == null || !getter.IsStatic)
                {
                    return null;
                }

                return property.GetValue(null, null);
            }

            if (member is FieldInfo field)
            {
                if (!field.IsStatic)
                {
                    return null;
                }

                return field.GetValue(null);
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static MemberInfo GetMember(string exactName)
    {
        if (accuracyStateType == null)
        {
            return null;
        }

        if (MemberCache.TryGetValue(exactName, out MemberInfo cached))
        {
            return cached;
        }

        PropertyInfo property = accuracyStateType.GetProperty(exactName, StaticAny);
        if (property != null && property.GetIndexParameters().Length == 0)
        {
            MemberCache[exactName] = property;
            return property;
        }

        FieldInfo field = accuracyStateType.GetField(exactName, StaticAny);
        if (field != null)
        {
            MemberCache[exactName] = field;
            return field;
        }

        MemberCache[exactName] = null;
        return null;
    }

    private static int ReadInt(string exactName)
    {
        return ToInt(ReadMember(exactName));
    }

    private static bool ReadBool(string exactName)
    {
        object value = ReadMember(exactName);
        if (value is bool boolValue)
        {
            return boolValue;
        }

        return false;
    }

    private static int ToInt(object value)
    {
        try
        {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }
}
