using Overlayer.Core;
using Overlayer.Tags.Attributes;
using Overlayer.Utils;
using System;
using System.Reflection;

namespace Overlayer.Tags;

public static class PlayerHitStats
{
    private const BindingFlags StaticPublic = BindingFlags.Public | BindingFlags.Static;

    private static readonly (string TagName, string MethodName)[] PlayerAwareOverrides = new[]
    {
        ("OTE", nameof(OTE)),
        ("OVE", nameof(OVE)),
        ("OEP", nameof(OEP)),
        ("OP", nameof(OP)),
        ("OLP", nameof(OLP)),
        ("OVL", nameof(OVL)),
        ("OTL", nameof(OTL)),
        ("OA", nameof(OA)),
        ("OPP", nameof(OPP)),
        ("OFast", nameof(OFast)),
        ("OSlow", nameof(OSlow)),
        ("OELP", nameof(OELP)),
        ("OV", nameof(OV)),
        ("OT", nameof(OT)),
        ("MissCount", nameof(MissCount)),
        ("Overloads", nameof(Overloads)),
        ("Fail", nameof(Fail)),
    };

    public static void RegisterOverrides()
    {
        foreach (var (tagName, methodName) in PlayerAwareOverrides)
        {
            MethodInfo method = typeof(PlayerHitStats).GetMethod(methodName, StaticPublic);
            if (method == null)
            {
                continue;
            }

            TagManager.SetTag(new OverlayerTag(method, new TagAttribute(tagName)));
        }
    }

    [TagDesc("读取玩家的Too Early次数，直接使用游戏自身计数，重生时Overlayer不会主动清零\n示例:{OTE:2}显示玩家2的次数")]
    public static int OTE(int player = 1) => Count(player, HitMargin.TooEarly);

    [TagDesc("读取玩家的Very Early次数，直接使用游戏自身计数，重生时Overlayer不会主动清零\n示例:{OVE:2}显示玩家2的次数")]
    public static int OVE(int player = 1) => Count(player, HitMargin.VeryEarly);

    [TagDesc("读取玩家的Early Perfect次数，直接使用游戏自身计数，重生时Overlayer不会主动清零\n示例:{OEP:2}显示玩家2的次数")]
    public static int OEP(int player = 1) => Count(player, HitMargin.EarlyPerfect);

    [TagDesc("读取玩家的Perfect和Auto总数，直接使用游戏自身计数\n示例:{OP:2}显示玩家2的总数")]
    public static int OP(int player = 1) => OPP(player) + OA(player);

    [TagDesc("读取玩家的Late Perfect次数，直接使用游戏自身计数，重生时Overlayer不会主动清零\n示例:{OLP:2}显示玩家2的次数")]
    public static int OLP(int player = 1) => Count(player, HitMargin.LatePerfect);

    [TagDesc("读取玩家的Very Late次数，直接使用游戏自身计数，重生时Overlayer不会主动清零\n示例:{OVL:2}显示玩家2的次数")]
    public static int OVL(int player = 1) => Count(player, HitMargin.VeryLate);

    [TagDesc("读取玩家的Too Late次数，直接使用游戏自身计数，重生时Overlayer不会主动清零\n示例:{OTL:2}显示玩家2的次数")]
    public static int OTL(int player = 1) => Count(player, HitMargin.TooLate);

    [TagDesc("读取玩家的Auto次数，直接使用游戏自身计数\n示例:{OA:2}显示玩家2的次数")]
    public static int OA(int player = 1) => Count(player, HitMargin.Auto);

    [TagDesc("读取玩家手动打出的Perfect次数，不包含Auto\n示例:{OPP:2}显示玩家2的次数")]
    public static int OPP(int player = 1) => Count(player, HitMargin.Perfect);

    [TagDesc("读取玩家的偏快判定总数，包含Too Early、Very Early和Early Perfect\n示例:{OFast:2}")]
    public static int OFast(int player = 1) => OTE(player) + OVE(player) + OEP(player);

    [TagDesc("读取玩家的偏慢判定总数，包含Too Late、Very Late和Late Perfect\n示例:{OSlow:2}")]
    public static int OSlow(int player = 1) => OTL(player) + OVL(player) + OLP(player);

    [TagDesc("读取玩家的Early Perfect和Late Perfect总数\n示例:{OELP:2}")]
    public static int OELP(int player = 1) => OEP(player) + OLP(player);

    [TagDesc("读取玩家的Very Early和Very Late总数\n示例:{OV:2}")]
    public static int OV(int player = 1) => OVE(player) + OVL(player);

    [TagDesc("读取玩家的Too Early和Too Late总数\n示例:{OT:2}")]
    public static int OT(int player = 1) => OTE(player) + OTL(player);

    [TagDesc("读取玩家的Miss次数，直接使用游戏自身计数，重生时Overlayer不会主动清零\n示例:{MissCount:2}")]
    public static int MissCount(int player = 1) => Count(player, HitMargin.FailMiss);

    [TagDesc("读取玩家的Overload次数，直接使用游戏自身计数，重生时Overlayer不会主动清零\n示例:{Overloads:2}")]
    public static int Overloads(int player = 1) => Count(player, HitMargin.FailOverload);

    [TagDesc("读取玩家的Miss和Overload总数\n示例:{Fail:2}")]
    public static int Fail(int player = 1) => MissCount(player) + Overloads(player);

    [Tag]
    [TagDesc("按判定名称读取玩家次数，直接使用游戏自身计数\n可用TooEarly、VeryEarly、EarlyPerfect、Perfect、LatePerfect、VeryLate、TooLate、Auto、FailMiss、FailOverload\n示例:{PlayerHit:VeryEarly:2}")]
    public static int PlayerHit(string margin, int player = 1)
    {
        return TryParseMargin(margin, out HitMargin parsed) ? Count(player, parsed) : 0;
    }

    [Tag]
    [TagDesc("读取玩家的有效击中总数，不包含Miss和Overload\n示例:{PlayerTotalHits:2}")]
    public static int PlayerTotalHits(int player = 1) => VersionSafe.GetHitMarginsTotal(ToPlayerIndex(player));

    [Tag]
    [TagDesc("按游戏当前判定计数计算玩家Accuracy\ndigits控制小数位，-1使用默认精度\n示例:{PlayerAccuracy:2:2}")]
    public static double PlayerAccuracy(int player = 1, int digits = -1) => Accuracy(player, digits);

    [Tag]
    [TagDesc("按游戏当前判定计数计算玩家XAccuracy，包含检查点惩罚\n示例:{PlayerXAccuracy:2:2}")]
    public static double PlayerXAccuracy(int player = 1, int digits = -1) => XAccuracy(player, false, digits);

    [Tag]
    [TagDesc("按游戏当前判定计数计算玩家绝对XAccuracy，不计算检查点惩罚\n示例:{PlayerAbsXAccuracy:2:2}")]
    public static double PlayerAbsXAccuracy(int player = 1, int digits = -1) => XAccuracy(player, true, digits);

    public static int ToPlayerIndex(int player)
    {
        return player <= 1 ? 0 : player - 1;
    }

    private static bool TryParseMargin(string margin, out HitMargin parsed)
    {
        parsed = default;
        return !string.IsNullOrWhiteSpace(margin) && Enum.TryParse(margin, true, out parsed);
    }

    private static int Count(int player, HitMargin margin)
    {
        return VersionSafe.GetHitCount(margin, ToPlayerIndex(player));
    }

    private static double Accuracy(int player, int digits)
    {
        int perfect = Count(player, HitMargin.Perfect);
        int auto = Count(player, HitMargin.Auto);
        int earlyPerfect = Count(player, HitMargin.EarlyPerfect);
        int latePerfect = Count(player, HitMargin.LatePerfect);
        int failMiss = Count(player, HitMargin.FailMiss);
        int failOverload = Count(player, HitMargin.FailOverload);

        int success = perfect + earlyPerfect + latePerfect + auto;
        int total = VersionSafe.GetHitMarginsTotal(ToPlayerIndex(player)) + failMiss + failOverload;
        if (total <= 0)
        {
            return 0;
        }

        double ratio = success == total ? 1.0 : (double)success / total;
        double bonus = (perfect + auto) * 0.0001;

        return (100.0 * (ratio + bonus)).Round(digits);
    }

    private static double XAccuracy(int player, bool absolute, int digits)
    {
        int perfect = Count(player, HitMargin.Perfect);
        int auto = Count(player, HitMargin.Auto);
        int earlyPerfect = Count(player, HitMargin.EarlyPerfect);
        int latePerfect = Count(player, HitMargin.LatePerfect);
        int veryEarly = Count(player, HitMargin.VeryEarly);
        int veryLate = Count(player, HitMargin.VeryLate);
        int tooEarly = Count(player, HitMargin.TooEarly);
        int tooLate = Count(player, HitMargin.TooLate);

        double totalHits = VersionSafe.GetHitMarginsTotal(ToPlayerIndex(player));
        if (totalHits <= 0)
        {
            return 0;
        }

        double weightedHits =
            perfect + auto +
            (0.75 * (earlyPerfect + latePerfect)) +
            (0.4 * (veryEarly + veryLate)) +
            (0.2 * (tooEarly + tooLate));

        double value = 100.0 * (weightedHits / totalHits);
        if (!absolute)
        {
            value *= Math.Pow(0.9875, VersionSafe.GetCheckpointsUsed());
        }

        return value.Round(digits);
    }
}
