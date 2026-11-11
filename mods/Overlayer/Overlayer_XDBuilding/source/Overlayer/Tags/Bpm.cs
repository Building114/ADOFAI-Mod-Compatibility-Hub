using Overlayer.Tags.Attributes;
using Overlayer.Utils;

namespace Overlayer.Tags;

public static class Bpm {
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("Tile BPM")]
    public static double TileBpm;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("The highest BPM in the level")]
    public static double MaxTileBpm => scnGame.instance != null ? scnGame.instance.highestBPM : 0d;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("Current BPM")]
    public static double CurBpm;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("Recommended KPS")]
    public static double RecKPS;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("Tile BPM (without pitch)")]
    public static double TileBpmWithoutPitch;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("Current BPM (without pitch)")]
    public static double CurBpmWithoutPitch;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("Recommended KPS (without pitch)")]
    public static double RecKPSWithoutPitch;

    public static float bpm, pitch, bpmwithoutpitch, playbackSpeed = 1;

    public static void Init(scrController __instance) {
        if(scnGame.instance == null && scnEditor.instance == null && !VersionSafe.IsGameWorld()) {
            return;
        }

        if(scnGame.instance != null) {
            pitch = (float)scnGame.instance.levelData.pitch / 100;
            if(ADOBase.isOfficialLevel) {
                pitch *= scrConductor.instance.song.pitch;
            }
            if(ADOBase.isCLSLevel) {
                pitch *= GCS.currentSpeedTrial;
            }
            if(scnEditor.instance != null) {
                pitch *= scnEditor.instance.playbackSpeed;
            }
            bpm = scnGame.instance.levelData.bpm * pitch;
            bpmwithoutpitch = scnGame.instance.levelData.bpm;
        } else {
            pitch = scrConductor.instance.song.pitch;
            bpm = scrConductor.instance.bpm * pitch;
            bpmwithoutpitch = scrConductor.instance.bpm;
        }
        float cur = bpm;
        float curWithoutPitch = bpmwithoutpitch;
        if(VersionSafe.GetCurrentSeqID(__instance) != 0) {
            double speed = VersionSafe.GetPlanetSpeed(scrController.instance);
            cur = (float)(bpm * speed);
            curWithoutPitch = (float)(bpmwithoutpitch * speed);
        }
        TileBpm = cur;
        CurBpm = cur;
        RecKPS = cur / 60;
        TileBpmWithoutPitch = curWithoutPitch;
        CurBpmWithoutPitch = curWithoutPitch;
        RecKPSWithoutPitch = curWithoutPitch / 60;
    }

    public static double GetRealBpm(scrFloor floor, float bpm) {
        if(floor == null) return bpm;

        double fallback = VersionSafe.GetPlanetSpeed(scrController.instance) * bpm;
        if(floor.nextfloor == null) return fallback;

        double interval = floor.nextfloor.entryTime - floor.entryTime;
        if(interval <= 0 || double.IsNaN(interval) || double.IsInfinity(interval)) return fallback;
        return 60.0 / interval;
    }

    public static void Update(scrFloor floor) {
        if(floor == null || floor.nextfloor == null) {
            return;
        }

        double curBPM = GetRealBpm(floor, bpm) * pitch;

        TileBpm = bpm * VersionSafe.GetPlanetSpeed(scrController.instance);
        CurBpm = curBPM;
        RecKPS = curBPM / 60;

        double curBPMWithoutPitch = GetRealBpm(floor, bpmwithoutpitch);
        TileBpmWithoutPitch = bpmwithoutpitch * VersionSafe.GetPlanetSpeed(scrController.instance);
        CurBpmWithoutPitch = curBPMWithoutPitch;
        RecKPSWithoutPitch = curBPMWithoutPitch / 60;
    }

    public static void Reset() {
        TileBpm = CurBpm = RecKPS = 0;
        TileBpmWithoutPitch = CurBpmWithoutPitch = RecKPSWithoutPitch = 0;
    }
}
