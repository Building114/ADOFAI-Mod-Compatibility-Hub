// XDB Patch modification: 2026-06-03
// Purpose: compatibility with ADOFAI r141+ field and method changes.
// Based on modlist-org/Overlayer-Lagacy 3.49.0, licensed under GPL-3.0.

using Overlayer.Core;
using Overlayer.Tags.Attributes;
using Overlayer.Utils;
using System;
using System.Reflection;

namespace Overlayer.Tags;

/// <summary>
/// Player-aware judgment tags for newer ADOFAI builds.
///
/// Text examples:
///   {OVE}     -> player 1 Very Early count
///   {OVE:2}   -> player 2 Very Early count
///   {OFast:2} -> player 2 fast-side count
///
/// The public tag argument is 1-based because that is what users expect in text.
/// Internally ADOFAI stores lists as 0-based, so player 2 becomes index 1.
/// </summary>
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

            // Replace the old no-argument tags with compatible methods.
            // Old layouts still work because each method defaults to player 1.
            TagManager.SetTag(new OverlayerTag(method, new TagAttribute(tagName)));
        }
    }

    public static int OTE(int player = 1) => Count(player, HitMargin.TooEarly);
    public static int OVE(int player = 1) => Count(player, HitMargin.VeryEarly);
    public static int OEP(int player = 1) => Count(player, HitMargin.EarlyPerfect);
    public static int OPP(int player = 1) => Count(player, HitMargin.Perfect);
    public static int OLP(int player = 1) => Count(player, HitMargin.LatePerfect);
    public static int OVL(int player = 1) => Count(player, HitMargin.VeryLate);
    public static int OTL(int player = 1) => Count(player, HitMargin.TooLate);
    public static int OA(int player = 1) => Count(player, HitMargin.Auto);

    // Keep Overlayer's old OP meaning: Perfect + Auto.
    public static int OP(int player = 1) => OPP(player) + OA(player);

    public static int OFast(int player = 1) => OTE(player) + OVE(player) + OEP(player);
    public static int OSlow(int player = 1) => OTL(player) + OVL(player) + OLP(player);
    public static int OELP(int player = 1) => OEP(player) + OLP(player);
    public static int OV(int player = 1) => OVE(player) + OVL(player);
    public static int OT(int player = 1) => OTE(player) + OTL(player);

    public static int MissCount(int player = 1) => Count(player, HitMargin.FailMiss);
    public static int Overloads(int player = 1) => Count(player, HitMargin.FailOverload);
    public static int Fail(int player = 1) => MissCount(player) + Overloads(player);

    [Tag]
    public static int PlayerHit(string margin, int player = 1)
    {
        if (string.IsNullOrWhiteSpace(margin) || !Enum.TryParse(margin, true, out HitMargin parsed))
        {
            return 0;
        }

        return Count(player, parsed);
    }

    [Tag]
    public static int PlayerTotalHits(int player = 1) => VersionSafe.GetHitMarginsTotal(ToPlayerIndex(player));

    [Tag]
    public static double PlayerAccuracy(int player = 1, int digits = -1) => Accuracy(player, digits);

    [Tag]
    public static double PlayerXAccuracy(int player = 1, int digits = -1) => XAccuracy(player, false, digits);

    [Tag]
    public static double PlayerAbsXAccuracy(int player = 1, int digits = -1) => XAccuracy(player, true, digits);

    public static int ToPlayerIndex(int player)
    {
        // {OVE}, {OVE:0}, and {OVE:1} all mean player 1.
        // {OVE:2} means player 2.
        return player <= 1 ? 0 : player - 1;
    }

    private static int Count(int player, HitMargin margin) => VersionSafe.GetHitCount(margin, ToPlayerIndex(player));

    private static double Accuracy(int player, int digits)
    {
        int perfect = Count(player, HitMargin.Perfect);
        int auto = Count(player, HitMargin.Auto);
        int earlyPerfect = Count(player, HitMargin.EarlyPerfect);
        int latePerfect = Count(player, HitMargin.LatePerfect);
        int failMiss = Count(player, HitMargin.FailMiss);
        int failOverload = Count(player, HitMargin.FailOverload);

        int success = perfect + earlyPerfect + latePerfect + auto;
        int total = VersionSafe.GetHitMarginsTotal(ToPlayerIndex(player)) + failMiss + failOverload;
        if (total <= 0)
        {
            return 0;
        }

        double ratio = success == total ? 1.0 : (double)success / total;
        double bonus = (perfect + auto) * 0.0001;

        return (100.0 * (ratio + bonus)).Round(digits);
    }

    private static double XAccuracy(int player, bool absolute, int digits)
    {
        int perfect = Count(player, HitMargin.Perfect);
        int auto = Count(player, HitMargin.Auto);
        int earlyPerfect = Count(player, HitMargin.EarlyPerfect);
        int latePerfect = Count(player, HitMargin.LatePerfect);
        int veryEarly = Count(player, HitMargin.VeryEarly);
        int veryLate = Count(player, HitMargin.VeryLate);
        int tooEarly = Count(player, HitMargin.TooEarly);
        int tooLate = Count(player, HitMargin.TooLate);

        double totalHits = VersionSafe.GetHitMarginsTotal(ToPlayerIndex(player));
        if (totalHits <= 0)
        {
            return 0;
        }

        double weightedHits =
            perfect + auto +
            (0.75 * (earlyPerfect + latePerfect)) +
            (0.4 * (veryEarly + veryLate)) +
            (0.2 * (tooEarly + tooLate));

        double value = 100.0 * (weightedHits / totalHits);
        if (!absolute)
        {
            value *= Math.Pow(0.9875, VersionSafe.GetCheckpointsUsed());
        }

        return value.Round(digits);
    }
}
