using Overlayer.Tags.Attributes;
using Overlayer.Utils;

namespace Overlayer.Tags;

public static class FailStats {

    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("The percentage of the overload gauge.")]
    public static float OverloadCounter() {
        var controller = scrController.instance;
        float counter = VersionSafe.GetFailCounter(controller, "overloadCounter");
        return float.IsNaN(counter)
            ? float.NaN
            : IsImmortal(controller) ? 100f : CalculateFailValue(counter);
    }

    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("The percentage of the multipress gauge.")]
    public static float MultipressCounter() {
        var controller = scrController.instance;
        float counter = VersionSafe.GetFailCounter(controller, "multipressCounter");
        return float.IsNaN(counter)
            ? float.NaN
            : IsImmortal(controller) ? 100f : CalculateFailValue(counter);
    }

    public static bool IsImmortal(scrController controller)
        => ADOBase.isOfficialLevel && VersionSafe.IsGameWorld(controller) && VersionSafe.GetPercentComplete(controller) >= 0.96f;

    public static float CalculateFailValue(float value)
        => value > 1f ? 0f : (1f - value) * 100f;

    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("The raw internal value of the overload gauge in the game.")]
    public static float OverloadCounterRaw => VersionSafe.GetFailCounter(scrController.instance, "overloadCounter");
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("The raw internal value of the multipress gauge in the game.")]
    public static float MultipressCounterRaw => VersionSafe.GetFailCounter(scrController.instance, "multipressCounter");
}
