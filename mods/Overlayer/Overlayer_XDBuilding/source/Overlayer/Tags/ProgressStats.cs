using Overlayer.Tags.Attributes;
using System;
using UnityEngine;
using Overlayer.Utils;

namespace Overlayer.Tags;

public static class ProgressStats {
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("Current progress")]
    public static double Progress => VersionSafe.GetPercentComplete() * 100;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("Current Actual Progress based on time")]
    public static double ActualProgress() {
        var listFloors = VersionSafe.GetLevelFloors();
        if(listFloors == null || listFloors.Count < 2) {
            return 0;
        }

        double firstFloorTime = VersionSafe.GetFloorEntryTime(listFloors[1]);
        double lastFloorTime = VersionSafe.GetFloorEntryTime(listFloors[listFloors.Count - 1]);
        double currentFloorTime = VersionSafe.GetFloorEntryTime(VersionSafe.GetCurrentFloor());
        double duration = lastFloorTime - firstFloorTime;

        if(double.IsNaN(firstFloorTime) || double.IsNaN(lastFloorTime) || double.IsNaN(currentFloorTime) || Math.Abs(duration) <= double.Epsilon) {
            return 0;
        }

        double actualProgress = (currentFloorTime - firstFloorTime) / duration * 100;
        return Mathf.Clamp((float)actualProgress, 0, 100);
    }
    [Tag]
    [TagDesc("Best progress of the level")]
    public static double BestProgress;

    public static void BestProgress_Reset() => BestProgress = 0;

    public static void BestProgress_Update() {
        if(scrLevelMaker.instance == null) {
            return;
        }
        BestProgress = Math.Max(BestProgress, VersionSafe.GetPercentComplete() * 100);
    }

    public static void BestProgress_Fix() {
        if(VersionSafe.IsGameWorld()) {
            BestProgress = 100;
        }
    }
}
