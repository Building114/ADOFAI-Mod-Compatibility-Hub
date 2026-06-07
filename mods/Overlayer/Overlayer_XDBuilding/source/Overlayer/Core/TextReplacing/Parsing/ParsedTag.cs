using Overlayer;
using Overlayer.Core;
using Overlayer.Core.TextReplacing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

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
            il.Emit(OpCodes.Call, ResolveArgumentMethod);
        }
        il.Emit(OpCodes.Call, tag.Getter);
    }

    [ThreadStatic]
    private static int resolveDepth;

    public static string ResolveArgument(string value) {
        if(string.IsNullOrEmpty(value) || value.IndexOf('{') < 0 || resolveDepth >= 8 || !TagManager.Initialized) {
            return value ?? string.Empty;
        }

        try {
            resolveDepth++;
            var availableTags = Main.IsPlaying ? TagManager.All : TagManager.NP;
            using ReplaceableText replaceable = ReplaceableText.Create(value, availableTags.Select(t => t.Tag));
            return replaceable.Replace() ?? value;
        } catch {
            return value;
        } finally {
            resolveDepth--;
        }
    }

    public static readonly MethodInfo ResolveArgumentMethod = typeof(ParsedTag).GetMethod(nameof(ResolveArgument), new[] { typeof(string) });
}
