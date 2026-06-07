using Overlayer.Core.TextReplacing.Parsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;

namespace Overlayer.Core.TextReplacing;

public class Replacer {
    public List<Tag> Tags { get; }
    public List<Tag> References { get; }
    private string source;
    private bool compiled;
    private Func<string> compiledMethod;
    private ReplaceableText interpretable;
    public Replacer(List<Tag> tags = null) {
        source = string.Empty;
        compiled = false;
        Tags = tags ?? [];
        References = [];
    }
    public Replacer(string source, List<Tag> tags = null) : this(tags) => Source = source;
    public Replacer(IEnumerable<Tag> tags = null) : this(tags?.ToList()) { }
    public Replacer(string source, IEnumerable<Tag> tags = null) : this(source, tags?.ToList()) { }
    public Replacer(Replacer other) {
        source = other.source;
        compiled = false;

        Tags = [.. other.Tags];
        References = [];
    }
    public string Source {
        get => source;
        set {
            value ??= string.Empty;
            if(source == value) {
                return;
            }

            ClearCompiledState();
            source = value;
        }
    }
    public Replacer Copy() => new(this);
    public string Replace() {
        if(!compiled) {
            if(!Compile()) {
                return null;
            }
        }

        return compiledMethod();
    }
    public string ReplaceI() => interpretable?.Replace();
    public bool Compile() {
        if(compiled) {
            return true;
        }

        try {
            ClearCompiledState();
            DynamicMethod dm = new(string.Empty, typeof(string), Type.EmptyTypes, typeof(Replacer), true);
            ILGenerator il = dm.GetILGenerator();
            interpretable = ReplaceableText.Create(source, Tags);
            il.Emit(OpCodes.Newobj, StrBuilder_Ctor);
            foreach(var parsed in interpretable.Replaceables) {
                switch(parsed) {
                    case ParsedTag pt:
                        pt.tag.ReferencedCount++;
                        References.Add(pt.tag);
                        parsed.Emit(il);
                        break;
                    case ParsedFormatTag pft:
                        pft.tag.ReferencedCount++;
                        References.Add(pft.tag);
                        parsed.Emit(il);
                        break;
                    default:
                        parsed.Emit(il);
                        break;
                }
                il.Emit(OpCodes.Call, StrBuilder_Append);
            }
            il.Emit(OpCodes.Call, StrBuilder_ToString);
            il.Emit(OpCodes.Ret);
            compiledMethod = (Func<string>)dm.CreateDelegate(typeof(Func<string>));
            return compiled = true;
        } catch(Exception e) {
            ClearCompiledState();
            Main.Logger.LogException(e);
            return false;
        }
    }
    public void UpdateTags(IEnumerable<Tag> tags) {
        ClearCompiledState();
        Tags.Clear();
        Tags.AddRange(tags ?? []);
    }
    private void ClearCompiledState() {
        try {
            interpretable?.Dispose();
        } catch { }
        interpretable = null;

        foreach(var tag in References.ToArray()) {
            try {
                tag.ReferencedCount--;
            } catch { }
        }
        References.Clear();
        compiledMethod = null;
        compiled = false;
    }
    public void Dispose() {
        ClearCompiledState();
    }
    public static readonly ConstructorInfo StrBuilder_Ctor = typeof(StringBuilder).GetConstructor(Type.EmptyTypes);
    public static readonly MethodInfo StrBuilder_Append = typeof(StringBuilder).GetMethod("Append", new[] { typeof(string) });
    public static readonly MethodInfo StrBuilder_ToString = typeof(StringBuilder).GetMethod("ToString", Type.EmptyTypes);
}
