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
        ("OPM", nameof(OPM)),
        ("OXP", nameof(OXP)),
        ("OXPerfect", nameof(OXPerfect)),
        ("OPPPlus", nameof(OPPPlus)),
        ("OPureXPerfect", nameof(OPureXPerfect)),
        ("OInnerPerfect", nameof(OInnerPerfect)),
        ("OOuterPerfect", nameof(OOuterPerfect)),
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
    public static int OTE(int player = 1) => Count(player, "TooEarly");

    [TagDesc("读取玩家的Very Early次数，直接使用游戏自身计数，重生时Overlayer不会主动清零\n示例:{OVE:2}显示玩家2的次数")]
    public static int OVE(int player = 1) => Count(player, "VeryEarly");

    [TagDesc("读取玩家的Early Perfect次数，直接使用游戏自身计数，重生时Overlayer不会主动清零\n示例:{OEP:2}显示玩家2的次数")]
    public static int OEP(int player = 1) => Count(player, "EarlyPerfect");

    [TagDesc("读取玩家的Perfect和Auto总数，直接使用游戏自身计数\n示例:{OP:2}显示玩家2的总数")]
    public static int OP(int player = 1) => OPP(player) + OA(player);

    [TagDesc("读取玩家的Late Perfect次数，直接使用游戏自身计数，重生时Overlayer不会主动清零\n示例:{OLP:2}显示玩家2的次数")]
    public static int OLP(int player = 1) => Count(player, "LatePerfect");

    [TagDesc("读取玩家的Very Late次数，直接使用游戏自身计数，重生时Overlayer不会主动清零\n示例:{OVL:2}显示玩家2的次数")]
    public static int OVL(int player = 1) => Count(player, "VeryLate");

    [TagDesc("读取玩家的Too Late次数，直接使用游戏自身计数，重生时Overlayer不会主动清零\n示例:{OTL:2}显示玩家2的次数")]
    public static int OTL(int player = 1) => Count(player, "TooLate");

    [TagDesc("读取玩家的Auto次数，直接使用游戏自身计数\n示例:{OA:2}显示玩家2的次数")]
    public static int OA(int player = 1) => Count(player, "Auto");

    [TagDesc("读取玩家手动Perfect总数，包含PerfectMinus、XPerfect和PerfectPlus，不包含Auto\n示例:{OPP:2}显示玩家2的次数")]
    public static int OPP(int player = 1) =>
        Count(player, "Perfect") +
        Count(player, "PerfectMinus") +
        Count(player, "XPerfect") +
        Count(player, "PerfectPlus");

    [TagDesc("读取玩家的PerfectMinus次数")]
    public static int OPM(int player = 1) => Count(player, "PerfectMinus");

    [TagDesc("读取玩家的XPerfect次数，不包含Auto")]
    public static int OXP(int player = 1) => Count(player, "XPerfect");

    [TagDesc("读取玩家的XPerfect总数，包含Auto")]
    public static int OXPerfect(int player = 1) => OXP(player) + OA(player);

    [TagDesc("读取玩家的PerfectPlus次数")]
    public static int OPPPlus(int player = 1) => Count(player, "PerfectPlus");

    [TagDesc("读取玩家的纯XPerfect次数，不包含Auto")]
    public static int OPureXPerfect(int player = 1) => OXP(player);

    [TagDesc("读取玩家的Inner Perfect总数，即PerfectMinus和PerfectPlus")]
    public static int OInnerPerfect(int player = 1) => OPM(player) + OPPPlus(player);

    [TagDesc("读取玩家的Outer Perfect总数，即EarlyPerfect和LatePerfect")]
    public static int OOuterPerfect(int player = 1) => OEP(player) + OLP(player);

    [TagDesc("读取玩家的偏快判定总数，包含Too Early、Very Early、Early Perfect和PerfectMinus\n示例:{OFast:2}")]
    public static int OFast(int player = 1) => OTE(player) + OVE(player) + OEP(player) + Count(player, "PerfectMinus");

    [TagDesc("读取玩家的偏慢判定总数，包含Too Late、Very Late、Late Perfect和PerfectPlus\n示例:{OSlow:2}")]
    public static int OSlow(int player = 1) => Count(player, "PerfectPlus") + OLP(player) + OVL(player) + OTL(player);

    [TagDesc("读取玩家的Early Perfect和Late Perfect总数\n示例:{OELP:2}")]
    public static int OELP(int player = 1) => OEP(player) + OLP(player);

    [TagDesc("读取玩家的Very Early和Very Late总数\n示例:{OV:2}")]
    public static int OV(int player = 1) => OVE(player) + OVL(player);

    [TagDesc("读取玩家的Too Early和Too Late总数\n示例:{OT:2}")]
    public static int OT(int player = 1) => OTE(player) + OTL(player);

    [TagDesc("读取玩家的Miss次数，直接使用游戏自身计数，重生时Overlayer不会主动清零\n示例:{MissCount:2}")]
    public static int MissCount(int player = 1) => Count(player, "FailMiss");

    [TagDesc("读取玩家的Overload次数，直接使用游戏自身计数，重生时Overlayer不会主动清零\n示例:{Overloads:2}")]
    public static int Overloads(int player = 1) => Count(player, "FailOverload");

    [TagDesc("读取玩家的Miss和Overload总数\n示例:{Fail:2}")]
    public static int Fail(int player = 1) => MissCount(player) + Overloads(player);

    [Tag]
    [TagDesc("读取玩家的PerfectMinus次数")]
    public static int PlayerPerfectMinus(int player = 1) => Count(player, "PerfectMinus");

    [Tag]
    [TagDesc("读取玩家的XPerfect次数，不包含Auto")]
    public static int PlayerXPerfect(int player = 1) => Count(player, "XPerfect");

    [Tag]
    [TagDesc("读取玩家的PerfectPlus次数")]
    public static int PlayerPerfectPlus(int player = 1) => Count(player, "PerfectPlus");

    [Tag]
    [TagDesc("按判定名称读取玩家次数，直接使用游戏自身计数\n可用TooEarly、VeryEarly、EarlyPerfect、Perfect、PerfectMinus、XPerfect、PerfectPlus、LatePerfect、VeryLate、TooLate、Multipress、FailMiss、FailOverload、Auto、OverPress、Midspin、FailedFloor\n示例:{PlayerHit:VeryEarly:2}")]
    public static int PlayerHit(string margin, int player = 1)
    {
        if (string.Equals(margin, "Perfect", StringComparison.OrdinalIgnoreCase))
        {
            return OPP(player);
        }

        return VersionSafe.GetHitCount(margin, ToPlayerIndex(player));
    }

    [Tag]
    [TagDesc("读取玩家的有效击中总数，不包含Miss和Overload\n示例:{PlayerTotalHits:2}")]
    public static int PlayerTotalHits(int player = 1) => VersionSafe.GetHitMarginsTotal(ToPlayerIndex(player));

    [Tag]
    [TagDesc("读取游戏官方计算的玩家Accuracy\n示例:{PlayerAccuracy:2}")]
    public static double PlayerAccuracy(int player = 1, int digits = -1) => (VersionSafe.GetPercentAcc(ToPlayerIndex(player)) * 100.0).Round(digits);

    [Tag]
    [TagDesc("读取游戏官方计算的玩家XAccuracy，包含检查点惩罚\n示例:{PlayerXAccuracy:2}")]
    public static double PlayerXAccuracy(int player = 1, int digits = -1) => (VersionSafe.GetPercentXAcc(ToPlayerIndex(player)) * 100.0).Round(digits);

    [Tag]
    [TagDesc("当前游戏没有提供不含检查点惩罚的独立XAccuracy数值")]
    public static double PlayerAbsXAccuracy(int player = 1, int digits = -1) => double.NaN;

    public static int ToPlayerIndex(int player)
    {
        return player < 1 ? -1 : player - 1;
    }

    private static int Count(int player, string marginName)
    {
        return VersionSafe.GetHitCount(marginName, ToPlayerIndex(player));
    }
}
