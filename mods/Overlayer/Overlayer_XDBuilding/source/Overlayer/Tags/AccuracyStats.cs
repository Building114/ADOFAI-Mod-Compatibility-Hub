using Overlayer.Tags.Attributes;
using Overlayer.Utils;

namespace Overlayer.Tags;

public static class AccuracyStats {
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("Official Accuracy")]
    public static double Accuracy => VersionSafe.GetPercentAcc() * 100.0;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("Official XAccuracy")]
    public static double XAccuracy => VersionSafe.GetPercentXAcc() * 100.0;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("Unavailable: XAccuracy without checkpoint penalties returns NaN")]
    public static double AbsXAccuracy => double.NaN;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("Unavailable: highest achievable Accuracy returns NaN")]
    public static double MaxAccuracy => double.NaN;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("Unavailable: maximum XAccuracy including checkpoint penalties returns NaN")]
    public static double MaxXAccuracy => double.NaN;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("Official maximum possible XAccuracy without checkpoint penalty")]
    public static double AbsMaxXAccuracy => VersionSafe.GetMaxPossibleXAcc() * 100.0;

    public static void Reset() { }
}
