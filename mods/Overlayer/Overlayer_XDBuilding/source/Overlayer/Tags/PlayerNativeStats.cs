using Overlayer.Tags.Attributes;
using Overlayer.Utils;

namespace Overlayer.Tags;

public static class PlayerNativeStats {
    [Tag]
    [TagDesc("官方当前XScore。玩家不存在时返回-1")]
    public static int PlayerXScore(int player = 1) => VersionSafe.GetNativeTrackerInt(PlayerHitStats.ToPlayerIndex(player), "xScore");

    [Tag]
    [TagDesc("检查点保存的XScore，而非最近一次击键得分！！")]
    public static int PlayerLastXScore(int player = 1) => VersionSafe.GetNativeTrackerInt(PlayerHitStats.ToPlayerIndex(player), "lastXScore");

    [Tag]
    [TagDesc("官方关卡理论最大XScore")]
    public static int PlayerMaxXScore(int player = 1) => VersionSafe.GetNativeTrackerInt(PlayerHitStats.ToPlayerIndex(player), "maxXScore");

    [Tag]
    [TagDesc("官方当前理论最高XAccuracy，百分比")]
    public static double PlayerMaxPossibleXAcc(int player = 1, int digits = -1) =>
        (VersionSafe.GetMaxPossibleXAcc(PlayerHitStats.ToPlayerIndex(player)) * 100.0).Round(digits);

    [Tag]
    [TagDesc("官方Accuracy，百分比，PlayerAccuracy别名")]
    public static double PlayerNativeAccuracy(int player = 1, int digits = -1) => PlayerHitStats.PlayerAccuracy(player, digits);

    [Tag]
    [TagDesc("官方XAccuracy，百分比，PlayerXAccuracy别名")]
    public static double PlayerNativeXAccuracy(int player = 1, int digits = -1) => PlayerHitStats.PlayerXAccuracy(player, digits);

    [Tag]
    [TagDesc("当前玩家累计死亡次数")]
    public static int PlayerDeaths(int player = 1) => VersionSafe.GetNativeTrackerInt(PlayerHitStats.ToPlayerIndex(player), "deaths");

    [Tag]
    [TagDesc("最近一次官方判定，返回原生名称")]
    public static string PlayerLastHitMargin(int player = 1) => VersionSafe.GetNativeLastMargin(PlayerHitStats.ToPlayerIndex(player));

    [Tag]
    [TagDesc("最近一次检查点保存的判定，返回原生名称")]
    public static string PlayerLastCheckpointHitMargin(int player = 1) => VersionSafe.GetNativeLastMargin(PlayerHitStats.ToPlayerIndex(player), true);

    [Tag]
    [TagDesc("当前玩家官方判定记录总条数，含失败")]
    public static int PlayerHitMarginsTotal(int player = 1) => VersionSafe.GetNativeRecordedMarginsTotal(PlayerHitStats.ToPlayerIndex(player));

    [Tag]
    [TagDesc("玩家是否保持官方认可的全Perfect记录，包含检查点限制（即是否会在结算页面显示完美无瑕）")]
    public static bool PlayerIsAllPurePerfect(int player = 1) => VersionSafe.GetNativeIsAllPurePerfect(PlayerHitStats.ToPlayerIndex(player));

    [Tag]
    [TagDesc("当前玩家完成进度，百分比")]
    public static double PlayerPercentComplete(int player = 1, int digits = -1) =>
        (VersionSafe.GetNativePercentComplete(PlayerHitStats.ToPlayerIndex(player)) * 100.0).Round(digits);

    [Tag]
    [TagDesc("当前玩家官方Accuracy，百分比")]
    public static double PlayerPercentAcc(int player = 1, int digits = -1) => PlayerHitStats.PlayerAccuracy(player, digits);

    [Tag]
    [TagDesc("当前玩家官方XAccuracy，百分比")]
    public static double PlayerPercentXAcc(int player = 1, int digits = -1) => PlayerHitStats.PlayerXAccuracy(player, digits);

    [Tag]
    [TagDesc("XPerfect在官方已记录玩家判定中的占比，百分比")]
    public static double PlayerXPerfectRate(int player = 1, int digits = -1) => PercentOfHits(player, "XPerfect", digits);

    [Tag]
    [TagDesc("PerfectPlus在官方已记录玩家判定中的占比，百分比")]
    public static double PlayerPerfectPlusRate(int player = 1, int digits = -1) => PercentOfHits(player, "PerfectPlus", digits);

    [Tag]
    [TagDesc("PerfectMinus在官方已记录玩家判定中的占比，百分比")]
    public static double PlayerPerfectMinusRate(int player = 1, int digits = -1) => PercentOfHits(player, "PerfectMinus", digits);

    [Tag]
    [TagDesc("玩家的+Perfect/XPerfect/-Perfect分布")]
    public static string PlayerPerfectDistribution(int player = 1) {
        int index = PlayerHitStats.ToPlayerIndex(player);
        return $"+{VersionSafe.GetHitCount("PerfectPlus", index)}/{VersionSafe.GetHitCount("XPerfect", index)}/-{VersionSafe.GetHitCount("PerfectMinus", index)}";
    }

    private static double PercentOfHits(int player, string margin, int digits) {
        int index = PlayerHitStats.ToPlayerIndex(player);
        int total = VersionSafe.GetNativeTrackerInt(index, "playerHitMarginCount");
        return total < 0 ? double.NaN : total == 0 ? 0 : (VersionSafe.GetHitCount(margin, index) * 100.0 / total).Round(digits);
    }
}
