using DG.Tweening;
using Overlayer.Core;
using Overlayer.Core.TextReplacing;
using Overlayer.Tags.Attributes;
using Overlayer.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Overlayer.Tags;

public static class Effect {
    [JSImplementedBy("Discord@kkitut")]
    [Tag(NotPlaying = true)]
    [TagDesc("以指定数值Tag或直接数字在两色间插值，可嵌套Wave、Calc等数值Tag\n示例:<color=#{ColorRange({Wave(0,100,1200)},0,100,FF6600,00AACC)}>Text</color>")]
    public static string ColorRange(string rawFunc, double valueMin, double valueMax, string colorMinHex, string colorMaxHex, Ease ease = Ease.Linear, int maxLength = -1, string afterTrimStr = Extensions.DefaultTrimStr) {
        if(string.IsNullOrWhiteSpace(rawFunc)) return "Tag Not Found!";

        double val;
        OverlayerTag ovTag = TagManager.GetTag(rawFunc);
        if(ovTag == null) {
            if(!double.TryParse(rawFunc, NumberStyles.Float, CultureInfo.InvariantCulture, out val))
                return "Tag Not Found!";
        } else {
            if(!ovTag.NotPlaying && !Main.IsPlaying) return "Tag Not Playing!";

            Delegate getter = ovTag.Tag.GetterDelegate;
            string source = getter is Func<string> fs ? fs() :
                getter is Func<string, string> fss ? fss("6") : null;
            if(source == null) return "Not Supported Tag!";
            if(!double.TryParse(source, NumberStyles.Float, CultureInfo.InvariantCulture, out val))
                return "Invalid numeric value!";
        }
        if(double.IsNaN(val) || double.IsInfinity(val) ||
           double.IsNaN(valueMin) || double.IsInfinity(valueMin) ||
           double.IsNaN(valueMax) || double.IsInfinity(valueMax) ||
           valueMin >= valueMax || valueMin < -float.MaxValue || valueMax > float.MaxValue) return "Invalid range!";
        val = Clamp(val, valueMin, valueMax);

        string hexMin = NormalizeHex(colorMinHex, out int fmtMin);
        string hexMax = NormalizeHex(colorMaxHex, out int fmtMax);
        if(fmtMin == -1 || fmtMax == -1 || hexMin == null || hexMax == null) {
            return "Invalid hex color!";
        }

        // Progress bars usually have 99+ fully filled/empty cells for every
        // partially filled one. Skip easing and color parsing for those cells.
        if(val <= valueMin) return OpaqueHexRGBA(hexMin).Trim(maxLength, afterTrimStr);
        if(val >= valueMax) return OpaqueHexRGBA(hexMax).Trim(maxLength, afterTrimStr);

        float eased = DOVirtual.EasedValue(0, 1, (float)((val - valueMin) / (valueMax - valueMin)), ease);
        if(!TryGetCachedColor(hexMin, out Color min) ||
           !TryGetCachedColor(hexMax, out Color max)) {
            return "Invalid hex color!";
        }
        Color newColor = new(
            ((1 - eased) * min.r) + (eased * max.r),
            ((1 - eased) * min.g) + (eased * max.g),
            ((1 - eased) * min.b) + (eased * max.b),
            ((1 - eased) * min.a) + (eased * max.a)
        );

        return ColorUtility.ToHtmlStringRGBA(newColor).Trim(maxLength, afterTrimStr);
    }
    private static readonly Dictionary<string, string> rgbaCache = new(StringComparer.OrdinalIgnoreCase);
    private static string OpaqueHexRGBA(string normalized) {
        if(rgbaCache.TryGetValue(normalized, out string html)) return html;
        html = (normalized.Length == 8 ? normalized : normalized + "FF").ToUpperInvariant();
        if(rgbaCache.Count < 512) rgbaCache[normalized] = html;
        return html;
    }

    // ColorRange often appears hundreds of times with identical endpoint colors.
    // Resolve each stable hex color once instead of reparsing it every frame.
    private static readonly Dictionary<string, Color> colorCache = new(StringComparer.OrdinalIgnoreCase);
    private static bool TryGetCachedColor(string hex, out Color color) {
        if(colorCache.TryGetValue(hex, out color)) return true;
        if(!ColorUtility.TryParseHtmlString("#" + hex, out color)) return false;
        if(colorCache.Count < 512) colorCache[hex] = color;
        return true;
    }

    public static double Clamp(double value, double min, double max) => value < min ? min : value > max ? max : value;
    private static readonly Dictionary<string, (string Hex, int Format)> normalizedHexCache = new(StringComparer.Ordinal);
    public static string NormalizeHex(string hex, out int fmt) {
        fmt = -1;
        if(string.IsNullOrWhiteSpace(hex)) return null;
        // Reuse normalized strings; otherwise #RRGGBBAA creates a fresh
        // substring for each ColorRange segment on every frame.
        if(normalizedHexCache.TryGetValue(hex, out var cached)) {
            fmt = cached.Format;
            return cached.Hex;
        }

        string raw = hex.Trim();
        if(raw.StartsWith("#")) raw = raw.Substring(1);
        if(raw.Length != 3 && raw.Length != 4 && raw.Length != 6 && raw.Length != 8) return null;
        foreach(char c in raw) {
            bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            if(!ok) return null;
        }

        string normalized;
        if(raw.Length == 8 || raw.Length == 6) {
            fmt = raw.Length;
            normalized = raw;
        } else if(raw.Length == 3) {
            fmt = 3;
            normalized = $"{raw[0]}{raw[0]}{raw[1]}{raw[1]}{raw[2]}{raw[2]}";
        } else {
            fmt = 4;
            normalized = $"{raw[0]}{raw[0]}{raw[1]}{raw[1]}{raw[2]}{raw[2]}{raw[3]}{raw[3]}";
        }
        if(normalizedHexCache.Count < 512) normalizedHexCache[hex] = (normalized, fmt);
        return normalized;
    }
    public static string FormatOutput(Color c, int fmt) {
        string h = ColorUtility.ToHtmlStringRGBA(c);
        return fmt == 8 ? h : fmt == 6 ? h.Substring(0, 6) : fmt == 3 ? $"{h[0]}{h[2]}{h[4]}" : fmt == 4 ? h : h;
    }

    static Dictionary<string, double> movingMan_tagValueCache = [];
    static Dictionary<string, long> movingMan_tagStartTimeCache = [];
    [JSImplementedBy("Discord@kkitut")]
    [Tag(NotPlaying = true)]
    [TagDesc("You can animate text on criterion certain tags. Used with <size>.")]
    public static double MovingMan(string rawFunc = nameof(ComboStats.Combo), double startSize = 30, double endSize = 80, double defaultSize = 30, double speed = 800, bool invert = false, Ease ease = Ease.OutExpo) {
        OverlayerTag ovTag = TagManager.GetTag(rawFunc);
        if(ovTag == null || (!ovTag.NotPlaying && !Main.IsPlaying)) {
            return defaultSize;
        }
        Tag tag = ovTag.Tag;
        Delegate getter = tag.GetterDelegate;

        double val = 0;
        if(getter is Func<string> fs) {
            val = StringConverter.ToDouble(fs());
        } else if(getter is Func<string, string> fss) {
            val = StringConverter.ToDouble(fss("6"));
        } else {
            return defaultSize;
        }

        movingMan_tagValueCache.TryGetValue(rawFunc, out double vCache);
        movingMan_tagStartTimeCache.TryGetValue(rawFunc, out long stCache);
        long mills = FastDateTime.Now.Ticks / 10000;
        if(val != vCache) {
            movingMan_tagStartTimeCache[rawFunc] = stCache = mills;
            movingMan_tagValueCache[rawFunc] = val;
        }
        float elapsed = mills - stCache;
        if(speed > 0 && !double.IsNaN(speed) && !double.IsInfinity(speed) && elapsed < speed) {
            float lifetime = (float)(elapsed / speed);
            float eased = DOVirtual.EasedValue(0, 1, lifetime, ease);
            if(invert) {
                eased = 1 - eased;
            }

            float changed = (float)(endSize - startSize) * eased;
            return startSize + changed;
        }

        return defaultSize;
    }

    [JSImplementedBy("Discord@wsbimango")]
    [Tag(NotPlaying = true)]
    [TagDesc("When the target tag value changes, the tag value changes according to the speed.")]
    public static double EasedValue(string rawFunc = nameof(Bpm.TileBpm), int digits = -1, double speed = 500, Ease ease = Ease.Linear) {
        OverlayerTag ovTag = TagManager.GetTag(rawFunc);
        if(ovTag == null || (!ovTag.NotPlaying && !Main.IsPlaying)) {
            return 0;
        }
        Tag tag = ovTag.Tag;

        Delegate getter = tag.GetterDelegate;
        EventEase ee = new(
            getter is Func<string> fs ?
            () => StringConverter.ToDouble(fs()) :
            getter is Func<string, string> fss ?
            () => StringConverter.ToDouble(fss("6")) :
            null, ease, speed, false);
        if(ee.Getter == null) {
            return 0;
        }

        if(speed <= 0 || double.IsNaN(speed) || double.IsInfinity(speed)) return ee.Value.Round(digits < 0 ? -1 : Math.Min(digits, 15));
        // Separate states for different speeds/easing curves. Do not sample the
        // source again after calculating the interpolation: Progress can change
        // during the frame, especially while rewinding.
        string key = rawFunc + "\u001f" + speed.ToString("R", CultureInfo.InvariantCulture) + "\u001f" + (int)ease;
        return ee.GetValue(key).Round(digits < 0 ? -1 : Math.Min(digits, 15));
    }

    [Tag(NotPlaying = true)]
    [TagDesc("Rainbow")]
    public static string Rainbow(double speed = 18) {
        if(speed <= 0 || double.IsNaN(speed) || double.IsInfinity(speed)) return "FFFFFF";
        double period = 360 * speed;
        if(double.IsInfinity(period) || period <= 0) return "FFFFFF";
        double hue = (Environment.TickCount % period + period) % period / speed;

        double c = 1;
        double x = 1 - Math.Abs(hue / 60 % 2 - 1);
        double m = 0;
        double r, g = 0, b;

        if(hue < 60) { r = c; g = x; b = 0; } else if(hue < 120) { r = x; g = c; b = 0; } else if(hue < 180) { r = 0; g = c; b = x; } else if(hue < 240) { r = 0; g = x; b = c; } else if(hue < 300) { r = x; g = 0; b = c; } else { r = c; g = 0; b = x; }

        int R = (int)((r + m) * 255);
        int G = (int)((g + m) * 255);
        int B = (int)((b + m) * 255);

        return $"{R:X2}{G:X2}{B:X2}";
    }

    private static double MotionPhase(double periodMs, double phase) {
        if(periodMs <= 0 || double.IsNaN(periodMs) || double.IsInfinity(periodMs) ||
           double.IsNaN(phase) || double.IsInfinity(phase)) return 0;
        double seconds = periodMs / 1000.0;
        if(seconds <= 0 || double.IsInfinity(seconds)) return 0;
        double cycle = (UnityEngine.Time.realtimeSinceStartupAsDouble % seconds) / seconds;
        double normalized = (cycle + phase % 1.0) % 1.0;
        if(double.IsNaN(normalized) || double.IsInfinity(normalized)) return 0;
        return normalized < 0 ? normalized + 1 : normalized;
    }

    private static bool ValidMotionRange(double min, double max) =>
        !double.IsNaN(min) && !double.IsInfinity(min) && !double.IsNaN(max) && !double.IsInfinity(max);

    [Tag(NotPlaying = true)]
    [TagDesc("平滑往返的数值动效，适合<size>等数值属性。周期单位毫秒\n示例:{Wave(24,36,1200)}")]
    public static string Wave(double min = 0, double max = 1, double periodMs = 1000, double phase = 0) {
        if(!ValidMotionRange(min, max)) return "0";
        double t = MotionPhase(periodMs, phase);
        double progress = (1 - Math.Cos(t * Math.PI * 2)) * 0.5;
        double value = min * (1 - progress) + max * progress;
        return (double.IsNaN(value) || double.IsInfinity(value) ? 0 : value).ToString("0.######", CultureInfo.InvariantCulture);
    }

    [Tag(NotPlaying = true)]
    [TagDesc("匀速往返的数值动效，适合<voffset>等数值属性。周期单位毫秒\n示例:{PingPong(-3,3,1500)}")]
    public static string PingPong(double min = 0, double max = 1, double periodMs = 1000, double phase = 0) {
        if(!ValidMotionRange(min, max)) return "0";
        double t = MotionPhase(periodMs, phase);
        double progress = 1 - Math.Abs(2 * t - 1);
        double value = min * (1 - progress) + max * progress;
        return (double.IsNaN(value) || double.IsInfinity(value) ? 0 : value).ToString("0.######", CultureInfo.InvariantCulture);
    }

    [Tag(NotPlaying = true)]
    [TagDesc("在两种颜色间平滑往返，返回8位RGBA十六进制颜色\n示例:<color=#{ColorPulse(FFAA55,5599FF,1200)}>Text</color>")]
    public static string ColorPulse(string startHex = "FFAA55", string endHex = "5599FF", double periodMs = 1000, double phase = 0) {
        string a = NormalizeHex(startHex, out _);
        string b = NormalizeHex(endHex, out _);
        if(a == null || b == null ||
           !TryGetCachedColor(a, out Color first) ||
           !TryGetCachedColor(b, out Color second)) return "FFFFFFFF";
        double t = MotionPhase(periodMs, phase);
        float progress = (float)((1 - Math.Cos(t * Math.PI * 2)) * 0.5);
        return ColorUtility.ToHtmlStringRGBA(Color.Lerp(first, second, progress));
    }

}
