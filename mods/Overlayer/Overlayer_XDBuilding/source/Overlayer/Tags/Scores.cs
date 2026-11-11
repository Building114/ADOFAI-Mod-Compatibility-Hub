using Overlayer.Tags.Attributes;

namespace Overlayer.Tags;

public static class Scores {
    [Tag]
    [TagDesc("Lenient difficulty score")]
    public static int LScore;
    [Tag]
    [TagDesc("Normal difficulty score")]
    public static int NScore;
    [Tag]
    [TagDesc("Strict difficulty score")]
    public static int SScore;
    [Tag]
    [TagDesc("Current difficulty score")]
    public static int Score;

    public static void SetScores(HitMargin l, HitMargin n, HitMargin s, HitMargin c) {
        LScore += ScoreFor(l);
        NScore += ScoreFor(n);
        SScore += ScoreFor(s);
        Score += ScoreFor(c);
    }

    private static int ScoreFor(HitMargin margin) {
        if(margin.IsPerfect()) return 300;
        return margin switch {
            HitMargin.EarlyPerfect or HitMargin.LatePerfect => 150,
            HitMargin.VeryEarly or HitMargin.VeryLate => 91,
            _ => 0,
        };
    }

    public static void Reset() => LScore = NScore = SScore = Score = 0;
}
