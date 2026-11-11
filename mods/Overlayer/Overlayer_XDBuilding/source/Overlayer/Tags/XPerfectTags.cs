using Overlayer.Tags.Attributes;
using Overlayer.Utils;
using System;

namespace Overlayer.Tags;

public static class XPerfectTags {
    [Tag(NotPlaying = true)]
    [TagDesc("当前游戏是否包含官方原生XPerfect判定")]
    public static bool XPerfectAvailable => Enum.IsDefined(typeof(HitMargin), "XPerfect");

    [Tag]
    [TagDesc("官方XPerfect次数")]
    public static int XPerfectCount() => VersionSafe.GetHitCount("XPerfect");

    [Tag]
    [TagDesc("官方PerfectPlus次数")]
    public static int XPlusPerfectCount() => VersionSafe.GetHitCount("PerfectPlus");

    [Tag]
    [TagDesc("官方PerfectMinus次数")]
    public static int XMinusPerfectCount() => VersionSafe.GetHitCount("PerfectMinus");

    [Tag]
    [TagDesc("官方三种Perfect的合计次数")]
    public static int XDetailedPerfectCount() => XPlusPerfectCount() + XPerfectCount() + XMinusPerfectCount();

    [Tag]
    [TagDesc("三种Perfect次数，格式为+完美次数/X完美次数/-完美次数")]
    public static string XPerfectDetail() => $"+{XPlusPerfectCount()}/{XPerfectCount()}/-{XMinusPerfectCount()}";

    [Tag]
    [TagDesc("游戏官方最近判定。False返回原生穷举名，True显示+Perfect/-Perfect等文本")]
    public static string XPerfectLastJudge(bool textJudge = true) {
        string margin = VersionSafe.GetNativeLastMargin(0);
        if(!textJudge) return margin;
        return margin switch {
            "PerfectPlus" => "+Perfect",
            "PerfectMinus" => "-Perfect",
            _ => margin,
        };
    }

    [Tag]
    [TagDesc("官方没有提供旧XPerfectMod的判定条消费标记，因此保留字段并返回False（虽然我也不知道为什么要整个这个玩意儿）")]
    public static bool XPerfectLastJudgeConsumedByMeter() => false;

    [Tag]
    [TagDesc("已有XPerfect且无其他判定、失败或从检查点开始")]
    public static bool XPerfectPureRun() => XPerfectCount() > 0 && VersionSafe.GetNativeIsAllPurePerfect(0) && VersionSafe.GetNativeRecordedMarginsTotal(0) == XPerfectCount();

    [Tag]
    [TagDesc("官方XPerfect占三种Perfect的百分比")]
    public static double XPerfectShare(int digits = -1) {
        int total = XDetailedPerfectCount();
        return total <= 0 ? 0 : (XPerfectCount() * 100.0 / total).Round(digits);
    }

    [Tag]
    [TagDesc("XPerfectShare的别名")]
    public static double XPerfectPercent(int digits = -1) => XPerfectShare(digits);

    [Tag("XPerfectRate")]
    [TagDesc("XPerfectShare的别名")]
    public static double XPerfectRate(int digits = -1) => XPerfectShare(digits);

    [Tag(NotPlaying = true)]
    [TagDesc("XPerfect数据来源，Native或Unavailable")]
    public static string XPerfectSource => XPerfectAvailable ? "Native" : "Unavailable";
}
