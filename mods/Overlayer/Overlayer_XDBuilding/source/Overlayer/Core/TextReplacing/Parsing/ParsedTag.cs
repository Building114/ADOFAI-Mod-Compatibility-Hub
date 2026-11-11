using Overlayer;
using Overlayer.Core;
using Overlayer.Core.TextReplacing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace Overlayer.Core.TextReplacing.Parsing;

public class ParsedTag : IParsed {
    public Tag tag;
    public List<string> args;
    public List<Tag> extraReferences;
    public IEnumerable<Tag> ReferencedTags => extraReferences == null ? new[] { tag } : new[] { tag }.Concat(extraReferences);
    public ParsedTag(Tag tag, List<string> args, List<Tag> extraReferences = null) {
        this.tag = tag;
        this.args = args;
        this.extraReferences = extraReferences ?? new List<Tag>();
    }
    public void Emit(ILGenerator il) {
        var parameters = tag.GetterOriginal.GetParameters();
        for(int i = 0; i < tag.ArgumentCount; i++) {
            string rawArg = args.Count - 1 < i
                ? parameters[i].DefaultValue?.ToString() ?? string.Empty
                : args[i];

            il.Emit(OpCodes.Ldstr, rawArg ?? string.Empty);
            if(ShouldResolveArgument(tag.Name, i)) {
                il.Emit(OpCodes.Call, ResolveArgumentMethod);
            }
        }
        il.Emit(OpCodes.Call, tag.Getter);
    }

    public static bool ShouldResolveArgument(string name, int index) =>
        !name.Equals("Coalesce", StringComparison.OrdinalIgnoreCase) &&
        !(name.Equals("IfExpr", StringComparison.OrdinalIgnoreCase) && index >= 1) &&
        !(name.Equals("IfText", StringComparison.OrdinalIgnoreCase) && index >= 3);

    [ThreadStatic]
    private static int resolveDepth;
    // Nested arguments are identical across many ColorRange segments. Cache the
    // resolved *result* within a frame, not a ReplaceableText retaining tag refs.
    [ThreadStatic]
    private static Dictionary<string, string> frameResults;
    [ThreadStatic]
    private static int frameResultsFrame;
    [ThreadStatic]
    private static int frameResultsGeneration;
    [ThreadStatic]
    private static bool frameResultsPlaying;

    public static string ResolveArgument(string value) {
        if(string.IsNullOrEmpty(value) || value.IndexOf('{') < 0 || resolveDepth >= 8 || !TagManager.Initialized) {
            return value ?? string.Empty;
        }

        bool playing = Main.IsPlaying;
        int frame = Time.frameCount;
        if(frameResults == null) frameResults = new Dictionary<string, string>(StringComparer.Ordinal);
        if(frameResultsFrame != frame || frameResultsGeneration != TagManager.Generation || frameResultsPlaying != playing) {
            frameResults.Clear();
            frameResultsFrame = frame;
            frameResultsGeneration = TagManager.Generation;
            frameResultsPlaying = playing;
        }
        bool cacheable = value.Length <= 512 && (
            value.StartsWith("{EasedValue(", StringComparison.Ordinal) ||
            value.StartsWith("{EasedValue:", StringComparison.Ordinal) ||
            value.StartsWith("{Wave(", StringComparison.Ordinal) ||
            value.StartsWith("{PingPong(", StringComparison.Ordinal) ||
            value.StartsWith("{PlayerPlanetColor(", StringComparison.Ordinal) ||
            value == "{Progress}" || value == "{BestProgress}" ||
            value == "{StartProgress}");
        if(cacheable && frameResults.TryGetValue(value, out string cached)) return cached;

        string result;
        try {
            resolveDepth++;
            var availableTags = playing ? TagManager.All : TagManager.NP;
            using ReplaceableText replaceable = ReplaceableText.Create(value, availableTags.Select(t => t.Tag));
            result = replaceable.Replace() ?? value;
        } catch {
            result = value;
        } finally {
            resolveDepth--;
        }
        if(cacheable && frameResults.Count < 512) frameResults[value] = result;
        return result;
    }

    public static readonly MethodInfo ResolveArgumentMethod = typeof(ParsedTag).GetMethod(nameof(ResolveArgument), new[] { typeof(string) });
}
