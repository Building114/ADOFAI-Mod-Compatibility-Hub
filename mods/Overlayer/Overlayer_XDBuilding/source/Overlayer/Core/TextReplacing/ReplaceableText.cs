using Overlayer.Core.TextReplacing.Lexing;
using Overlayer.Core.TextReplacing.Parsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Overlayer.Core.TextReplacing;

public class ReplaceableText : IDisposable {
    private bool disposed;
    public List<IParsed> Replaceables { get; private set; }
    public ReplaceableText(IEnumerable<IParsed> replaceables) {
        Replaceables = replaceables.ToList();
        Replaceables.ForEach(p => {
            if(p is ParsedTag tag) {
                foreach(var referenced in tag.ReferencedTags) {
                    referenced.ReferencedCount++;
                }
            } else if(p is ParsedFormatTag formatTag) {
                formatTag.tag.ReferencedCount++;
            }
        });
    }
    public string Replace() {
        return disposed
            ? throw new ObjectDisposedException(GetType().FullName)
            : Replaceables.Aggregate(new StringBuilder(), (sb, p) => {
                if(p is ParsedString str) {
                    sb.Append(str.str);
                } else if(p is ParsedTag tag) {
                    sb.Append(InvokeTag(tag.tag, BuildInvokeArguments(tag.tag, tag.args)));
                } else if(p is ParsedFormatTag formatTag) {
                    object target = formatTag.tag.GetterRaw.IsStatic ? null : formatTag.tag.GetterOriginalTarget;
                    object value = formatTag.tag.GetterRaw.Invoke(target, null);
                    sb.Append(formatTag.formatType == Tag.FormatType.Float
                        ? ParsedFormatTag.FloatToString(Convert.ToSingle(value), formatTag.format)
                        : ParsedFormatTag.DoubleToString(Convert.ToDouble(value), formatTag.format));
                }

                return sb;
            }).ToString();
    }

    public static string[] BuildInvokeArguments(Tag tag, IList<string> args) {
        var parameters = tag.GetterOriginal.GetParameters();
        string[] resolved = new string[tag.ArgumentCount];
        for(int i = 0; i < resolved.Length; i++) {
            string raw = args != null && args.Count > i
                ? args[i]
                : parameters.Length > i ? parameters[i].DefaultValue?.ToString() ?? string.Empty : string.Empty;
            resolved[i] = ParsedTag.ResolveArgument(raw);
        }
        return resolved;
    }

    public static object InvokeTag(Tag tag, params string[] args) => tag.Getter.Invoke(null, args);
    public static ReplaceableText Create(string source, IEnumerable<Tag> tags, LexConfig config = null) => new(Parser.ParseText(source, tags?.ToList() ?? [], config));
    public void Dispose() {
        if(disposed) {
            return;
        }

        Replaceables.ForEach(p => {
            if(p is ParsedTag tag) {
                foreach(var referenced in tag.ReferencedTags) {
                    referenced.ReferencedCount--;
                }
            } else if(p is ParsedFormatTag formatTag) {
                formatTag.tag.ReferencedCount--;
            }
        });
        Replaceables = null;
        GC.SuppressFinalize(this);
        disposed = true;
    }
    ~ReplaceableText() => Dispose();
}
