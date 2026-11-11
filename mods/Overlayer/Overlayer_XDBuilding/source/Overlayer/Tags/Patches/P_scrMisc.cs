using Overlayer.Core.Patches;
using Overlayer.Utils;
using System;
using System.Runtime.CompilerServices;

namespace Overlayer.Tags.Patches;

public class P_scrMisc : PatchBase<P_scrMisc> {
    private sealed class ProcessedHitState {
        public int HighestObservedCount = -1;
        public int LastProcessedCount = -1;
    }

    private static readonly object ObservationLock = new();
    private static ConditionalWeakTable<scrMarginTracker, ProcessedHitState> observations = new();

    public static void ResetObservations() {
        lock(ObservationLock) {
            observations = new ConditionalWeakTable<scrMarginTracker, ProcessedHitState>();
        }
    }

    private static void ObserveBeforeHit(scrMarginTracker tracker, int recordedCount) {
        lock(ObservationLock) {
            var state = observations.GetValue(tracker, _ => new ProcessedHitState());
            if(state.HighestObservedCount > recordedCount) {
                // The game can roll the same tracker back after a checkpoint.
                state.LastProcessedCount = -1;
            }
            state.HighestObservedCount = recordedCount;
        }
    }

    private static bool TryProcessHit(scrMarginTracker tracker, int currentCount) {
        lock(ObservationLock) {
            var state = observations.GetValue(tracker, _ => new ProcessedHitState());
            if(state.LastProcessedCount >= currentCount) {
                return false;
            }
            state.LastProcessedCount = currentCount;
            state.HighestObservedCount = Math.Max(state.HighestObservedCount, currentCount);
            return true;
        }
    }

    public sealed class HitState {
        public scrMarginTracker Tracker;
        public scrController Controller;
        public bool WasSafeFloor;
        public int RecordedCount;
        public HitMargin Lenient;
        public HitMargin Normal;
        public HitMargin Strict;
        public double TimingMs;
    }

    [LazyPatch("Tags.P_scrMisc.SwitchChosen", "scrPlanet", "SwitchChosen", Triggers = new[] {
        nameof(Hit.LHit), nameof(Hit.NHit), nameof(Hit.SHit), nameof(Hit.CHit),
        "LHitRaw", "NHitRaw", "SHitRaw", "CHitRaw",
        nameof(Hit.LTE), nameof(Hit.LVE), nameof(Hit.LEP), nameof(Hit.LP), nameof(Hit.LLP), nameof(Hit.LVL), nameof(Hit.LTL),
        nameof(Hit.NTE), nameof(Hit.NVE), nameof(Hit.NEP), nameof(Hit.NP), nameof(Hit.NLP), nameof(Hit.NVL), nameof(Hit.NTL),
        nameof(Hit.STE), nameof(Hit.SVE), nameof(Hit.SEP), nameof(Hit.SP), nameof(Hit.SLP), nameof(Hit.SVL), nameof(Hit.STL),
        nameof(Hit.CTE), nameof(Hit.CVE), nameof(Hit.CEP), nameof(Hit.CP), nameof(Hit.CLP), nameof(Hit.CVL), nameof(Hit.CTL),
        nameof(Hit.LFast), nameof(Hit.NFast), nameof(Hit.SFast), nameof(Hit.CFast),
        nameof(Hit.LSlow), nameof(Hit.NSlow), nameof(Hit.SSlow), nameof(Hit.CSlow),
        nameof(Hit.LT), nameof(Hit.LV), nameof(Hit.LELP), nameof(Hit.NT), nameof(Hit.NV), nameof(Hit.NELP),
        nameof(Hit.ST), nameof(Hit.SV), nameof(Hit.SELP), nameof(Hit.CT), nameof(Hit.CV), nameof(Hit.CELP),
        nameof(HitTiming.Timing), nameof(HitTiming.TimingAvg),
        nameof(ComboStats.Combo), nameof(ComboStats.MaxCombo),
        nameof(ComboStats.LMarginCombo), nameof(ComboStats.NMarginCombo), nameof(ComboStats.SMarginCombo), nameof(ComboStats.MarginCombo),
        nameof(ComboStats.LMarginMaxCombo), nameof(ComboStats.NMarginMaxCombo), nameof(ComboStats.SMarginMaxCombo), nameof(ComboStats.MarginMaxCombo),
        nameof(ComboStats.LMarginCombos), nameof(ComboStats.NMarginCombos), nameof(ComboStats.SMarginCombos), nameof(ComboStats.MarginCombos),
        nameof(ComboStats.LMarginMaxCombos), nameof(ComboStats.NMarginMaxCombos), nameof(ComboStats.SMarginMaxCombos), nameof(ComboStats.MarginMaxCombos),
        nameof(ComboStats.SpecialPlayMark),
        nameof(Scores.LScore), nameof(Scores.NScore), nameof(Scores.SScore), nameof(Scores.Score)
    })]
    public static class SwitchChosen {
        public static void Prefix(scrPlanet __instance, long? hitTick, out HitState __state) {
            __state = null;
            if(!Main.IsPlaying || __instance == null || !__instance.isChosen ||
               __instance.player == null || __instance.currfloor == null || __instance.planetarySystem == null) return;

            var controller = scrController.instance;
            var conductor = scrConductor.instance;
            if(controller == null || conductor == null || conductor.song == null ||
               VersionSafe.IsFreeroamFloor(__instance.currfloor)) return;

            var tracker = __instance.player.marginTracker;
            if(tracker == null) return;

            float bpm = (float)((double)conductor.bpm * __instance.planetarySystem.speed);
            float pitch = conductor.song.pitch;
            double scale = __instance.currfloor.nextfloor?.marginScale ?? 1.0;
            Func<Difficulty, HitMargin> judge;
            double timing;
            if(hitTick.HasValue) {
                double entryTime = __instance.currfloor.nextfloor?.entryTime ?? __instance.currfloor.entryTime;
                double songPosition = AsyncInputUtils.GetSongPositionAt(conductor, entryTime);
                double difference = (double)(hitTick.Value - AsyncInputManager.offsetTick) / 10000000.0 - songPosition;
                judge = diff => scrMisc.GetHitMarginInSec(diff, difference, bpm, pitch, scale);
                timing = difference * 1000.0;
            } else {
                float angle = (float)__instance.cachedAngle;
                float reference = (float)__instance.targetExitAngle;
                bool clockwise = __instance.planetarySystem.isCW;
                judge = diff => scrMisc.GetHitMarginInDeg(diff, angle, reference, clockwise, bpm, pitch, scale);
                double speedBpm = (double)conductor.bpm * __instance.planetarySystem.speed;
                timing = (__instance.cachedAngle - __instance.targetExitAngle) * (clockwise ? 1.0 : -1.0) * 60000.0 / (Math.PI * speedBpm * pitch);
            }

            int recordedCount = tracker.hitMargins.Count;
            ObserveBeforeHit(tracker, recordedCount);
            __state = new HitState {
                Tracker = tracker,
                Controller = controller,
                WasSafeFloor = VersionSafe.IsSafeFloor(__instance.currfloor),
                RecordedCount = recordedCount,
                Lenient = judge(Difficulty.Lenient),
                Normal = judge(Difficulty.Normal),
                Strict = judge(Difficulty.Strict),
                TimingMs = RDC.auto && !RDC.useOldAuto ? 0 : timing
            };
        }

        public static void Postfix(HitState __state) {
            if(__state == null) return;
            int currentCount = __state.Tracker.hitMargins.Count;
            if(currentCount <= __state.RecordedCount || !TryProcessHit(__state.Tracker, currentCount)) return;

            HitMargin actual = __state.Tracker.lastAddedHitMargin ?? HitMargin.XPerfect;
            Hit.Lenient = __state.Lenient;
            Hit.Normal = __state.Normal;
            Hit.Strict = __state.Strict;
            Hit.Current = actual;
            HitTiming.Timing = __state.TimingMs;
            HitTiming.Timings.Add(HitTiming.Timing);
            int timingCount = HitTiming.Timings.Count;
            HitTiming.TimingAvg += (HitTiming.Timing - HitTiming.TimingAvg) / timingCount;

            if(actual is HitMargin.Auto or HitMargin.Midspin or HitMargin.Multipress or
                HitMargin.FailMiss or HitMargin.FailOverload or HitMargin.OverPress or HitMargin.FailedFloor) {
                Hit.Lenient = Hit.Normal = Hit.Strict = actual;
            } else if(RDC.auto && !RDC.useOldAuto) {
                Hit.Lenient = Hit.Normal = Hit.Strict = HitMargin.XPerfect;
            } else if(__state.Controller.noFailInfiniteMargin) {
                Hit.Lenient = Hit.Normal = Hit.Strict = HitMargin.FailMiss;
            }

            if(__state.WasSafeFloor) return;
            Hit.IncreaseCount(Difficulty.Lenient, Hit.Lenient);
            Hit.IncreaseCount(Difficulty.Normal, Hit.Normal);
            Hit.IncreaseCount(Difficulty.Strict, Hit.Strict);
            Hit.IncreaseCCount(Hit.Current);

            if(Hit.Current.IsPerfect()) {
                ComboStats.MaxCombo = Math.Max(ComboStats.MaxCombo, ++ComboStats.Combo);
            } else {
                ComboStats.Combo = 0;
            }
            Scores.SetScores(Hit.Lenient, Hit.Normal, Hit.Strict, Hit.Current);
            ComboStats.Combos_Set(Difficulty.Lenient, Hit.Lenient);
            ComboStats.Combos_Set(Difficulty.Normal, Hit.Normal);
            ComboStats.Combos_Set(Difficulty.Strict, Hit.Strict);
            ComboStats.SetMarginCombos();
        }
    }
}
