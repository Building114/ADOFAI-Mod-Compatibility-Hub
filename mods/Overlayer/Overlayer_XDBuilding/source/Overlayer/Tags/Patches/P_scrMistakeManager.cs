                                     
                                                                     
                                                                        

using Overlayer.Core.Patches;
using Overlayer.Utils;
using System;

namespace Overlayer.Tags.Patches;

public class P_scrMistakeManager : PatchBase<P_scrMistakeManager>
{
    [LazyPatch("Tags.P_scrMistakeManager.AccuracyStats__CalculatePercentAcc_R136", "scrMistakesManager", "CalculatePercentAcc", MaxVersion = 140, Triggers = new string[] {
        nameof(AccuracyStats.Accuracy), nameof(AccuracyStats.MaxAccuracy),
        nameof(AccuracyStats.XAccuracy), nameof(AccuracyStats.MaxXAccuracy),
        nameof(AccuracyStats.AbsXAccuracy), nameof(AccuracyStats.AbsMaxXAccuracy),
    })]
    [LazyPatch("Tags.P_scrMistakeManager.AccuracyStats__CalculatePercentAcc_R141", "scrMarginTracker", "CalculatePercentAcc", MinVersion = 141, Triggers = new string[] {
        nameof(AccuracyStats.Accuracy), nameof(AccuracyStats.MaxAccuracy),
        nameof(AccuracyStats.XAccuracy), nameof(AccuracyStats.MaxXAccuracy),
        nameof(AccuracyStats.AbsXAccuracy), nameof(AccuracyStats.AbsMaxXAccuracy),
    })]
    public static class AccuracyStats__CalculatePercentAcc
    {
        public static void Postfix()
        {
            int perfect = VersionSafe.GetHitCount(HitMargin.Perfect);
            int auto = VersionSafe.GetHitCount(HitMargin.Auto);
            int earlyPerfect = VersionSafe.GetHitCount(HitMargin.EarlyPerfect);
            int latePerfect = VersionSafe.GetHitCount(HitMargin.LatePerfect);
            int veryEarly = VersionSafe.GetHitCount(HitMargin.VeryEarly);
            int veryLate = VersionSafe.GetHitCount(HitMargin.VeryLate);
            int tooEarly = VersionSafe.GetHitCount(HitMargin.TooEarly);
            int tooLate = VersionSafe.GetHitCount(HitMargin.TooLate);
            int failMiss = VersionSafe.GetHitCount(HitMargin.FailMiss);
            int failOverload = VersionSafe.GetHitCount(HitMargin.FailOverload);

            int success = perfect + earlyPerfect + latePerfect + auto;
            int total = VersionSafe.GetHitMarginsTotal() + failMiss + failOverload;

            if (total <= 0)
            {
                AccuracyStats.Accuracy = 0;
                AccuracyStats.XAccuracy = 0;
                AccuracyStats.AbsXAccuracy = 0;
                AccuracyStats.MaxAccuracy = 0;
                AccuracyStats.MaxXAccuracy = 0;
                AccuracyStats.AbsMaxXAccuracy = 0;
                return;
            }

            double ratio = success == total ? 1.0 : (double)success / total;
            double bonus = (perfect + auto) * 0.0001;

            AccuracyStats.Accuracy = 100.0 * (ratio + bonus);

            double totalHits = VersionSafe.GetHitMarginsTotal();
            if (totalHits <= 0)
            {
                AccuracyStats.XAccuracy = 0;
                AccuracyStats.AbsXAccuracy = 0;
                return;
            }

            double weightedHits =
                perfect + auto +
                (0.75 * (earlyPerfect + latePerfect)) +
                (0.4 * (veryEarly + veryLate)) +
                (0.2 * (tooEarly + tooLate));

            double checkpointMinus = Math.Pow(0.9875, VersionSafe.GetCheckpointsUsed());
            AccuracyStats.AbsXAccuracy = 100.0 * (weightedHits / totalHits);
            AccuracyStats.XAccuracy = AccuracyStats.AbsXAccuracy * checkpointMinus;

            var floors = VersionSafe.GetLevelFloors();
            if (floors != null &&
               Tile.CurTile >= 0 &&
               Tile.CurTile < floors.Count &&
               floors[Tile.CurTile] is scrFloor currentFloor)
            {

                int leftTile = Tile.LeftTile - 1 - (VersionSafe.IsMidSpinFloor(currentFloor) ? 1 : 0);

                int maxSuccess = leftTile + perfect + auto + earlyPerfect + latePerfect;
                int maxTotal = VersionSafe.GetHitMarginsTotal() + leftTile + failMiss + failOverload;

                double maxRatio = maxSuccess == maxTotal ? 1.0 : (double)maxSuccess / maxTotal;
                double maxBonus = (leftTile + perfect + auto) * 0.0001;

                AccuracyStats.MaxAccuracy = 100.0 * (maxRatio + maxBonus);

                double possibleHitsX =
                    leftTile + perfect + auto + Tile.StartTile - 1 +
                    (0.75 * (earlyPerfect + latePerfect)) +
                    (0.4 * (veryEarly + veryLate)) +
                    (0.2 * (tooEarly + tooLate));

                double denomX = Tile.TotalTile - 1 + tooEarly + tooLate;

                if (denomX > 0)
                {
                    AccuracyStats.AbsMaxXAccuracy = 100.0 * (possibleHitsX / denomX);
                    AccuracyStats.MaxXAccuracy = AccuracyStats.AbsMaxXAccuracy * checkpointMinus;
                }
            }
        }
    }
}
