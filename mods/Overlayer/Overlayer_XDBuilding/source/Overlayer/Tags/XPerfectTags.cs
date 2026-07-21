using Overlayer.Tags.Attributes;
using Overlayer.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace Overlayer.Tags;

             
                                                   
   
                                                               
                                             
                                          
                                              
                                      
                                             
                                                     
   
                                                                              
                                                                              
                         
   
                                                                           
                                                 
              
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
    [TagDesc("检查XPerfect是否已加载并且可以读取数据\n可配合IfExpr或IfText显示备用内容")]
    public static bool XPerfectAvailable => TryEnsureAccuracyStateType();

    [Tag]
    [TagDesc("读取XPerfect记录的正中Perfect次数")]
    public static int XPerfectCount()
    {
        return ReadInt("XPerfectCount");
    }

    [Tag]
    [TagDesc("读取XPerfect记录的+Perfect次数")]
    public static int XPlusPerfectCount()
    {
        return ReadInt("PlusPerfectCount");
    }

    [Tag]
    [TagDesc("读取XPerfect记录的-Perfect次数")]
    public static int XMinusPerfectCount()
    {
        return ReadInt("MinusPerfectCount");
    }

    [Tag]
    [TagDesc("读取+Perfect、正中Perfect和-Perfect的总数")]
    public static int XDetailedPerfectCount()
    {
        return XPlusPerfectCount() + XPerfectCount() + XMinusPerfectCount();
    }

    [Tag]
    [TagDesc("把XPerfect的三类Perfect合并成一段文本\n输出格式为+次数/正中次数/-次数")]
    public static string XPerfectDetail()
    {
        return $"+{XPlusPerfectCount()}/{XPerfectCount()}/-{XMinusPerfectCount()}";
    }

    [Tag]
    [TagDesc("读取XPerfect最近一次判定\n默认读取适合显示的LastJudgeForText，传入False时读取原始LastJudge\n示例:{XPerfectLastJudge}或{XPerfectLastJudge:False}")]
    public static string XPerfectLastJudge(bool textJudge = true)
    {
        object value = ReadMember(textJudge ? "LastJudgeForText" : "LastJudge");
        return value?.ToString() ?? string.Empty;
    }

    [Tag]
    [TagDesc("最近一次XPerfect判定是否已被判定条消费\n适合脚本避免同一次判定被重复处理")]
    public static bool XPerfectLastJudgeConsumedByMeter()
    {
        return ReadBool("LastJudgeConsumedByMeter");
    }

    [Tag]
    [TagDesc("当前记录是否只有正中Perfect且至少出现过一次判定\n可用于纯正中Perfect提示")]
    public static bool XPerfectPureRun()
    {
        return XDetailedPerfectCount() > 0 &&
            XPerfectCount() > 0 &&
            XPlusPerfectCount() == 0 &&
            XMinusPerfectCount() == 0;
    }

    [Tag]
    [TagDesc("正中Perfect占三类Perfect总数的百分比\ndigits控制小数位，-1使用默认精度\n示例:{XPerfectShare:2}")]
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
    [TagDesc("XPerfectShare的易读别名，返回正中Perfect占比\n示例:{XPerfectPercent:2}")]
    public static double XPerfectPercent(int digits = -1)
    {
                                                                 
                                                          
        return XPerfectShare(digits);
    }

    [Tag("XPerfectRate")]
    [TagDesc("XPerfectPercent的简写，返回正中Perfect占比\n示例:{XPerfectRate:2}")]
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
