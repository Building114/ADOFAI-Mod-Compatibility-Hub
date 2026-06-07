using Overlayer.Core.TextReplacing.Lexing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Overlayer.Core.TextReplacing.Parsing;

public static class Parser {
    public static IEnumerable<IParsed> Parse(string input, List<Tag> tags) => ParseText(input, tags);

    public static IEnumerable<IParsed> Parse(IEnumerable<Token> tokens, List<Tag> tags, LexConfig config = null) {
        config ??= new LexConfig();
        return ParseTokensLegacy(tokens, tags ?? [], config);
    }

    public static IEnumerable<IParsed> ParseText(string source, List<Tag> tags, LexConfig config = null) {
        config ??= new LexConfig();
        source ??= string.Empty;
        tags ??= [];

        StringBuilder literal = new();
        int i = 0;
        while(i < source.Length) {
            char c = source[i];

            if(c == '\\') {
                if(i + 1 < source.Length) {
                    literal.Append(source[i + 1]);
                    i += 2;
                } else {
                    literal.Append(c);
                    i++;
                }
                continue;
            }

            if(c != config.TagStart) {
                literal.Append(c);
                i++;
                continue;
            }

            if(i + 1 < source.Length && source[i + 1] == config.TagStart) {
                literal.Append(config.TagStart);
                i += 2;
                continue;
            }

            if(TryParseTag(source, i, tags, config, out IParsed parsed, out int nextIndex)) {
                if(literal.Length > 0) {
                    yield return new ParsedString(literal.ToString());
                    literal.Clear();
                }

                yield return parsed;
                i = nextIndex;
                continue;
            }

            literal.Append(c);
            i++;
        }

        if(literal.Length > 0) {
            yield return new ParsedString(literal.ToString());
        }
    }

    private static bool TryParseTag(string source, int openIndex, List<Tag> tags, LexConfig config, out IParsed parsed, out int nextIndex) {
        parsed = null;
        nextIndex = openIndex + 1;

        int closeIndex = FindMatchingTagEnd(source, openIndex, config);
        if(closeIndex < 0) {
            return false;
        }

        string fullText = source.Substring(openIndex, closeIndex - openIndex + 1);
        string content = source.Substring(openIndex + 1, closeIndex - openIndex - 1);
        if(content.Length == 0) {
            parsed = new ParsedString(fullText);
            nextIndex = closeIndex + 1;
            return true;
        }

        if(!TrySplitTagContent(content, config, out string tagName, out List<string> arguments, out bool isFormat)) {
            parsed = new ParsedString(fullText);
            nextIndex = closeIndex + 1;
            return true;
        }

        Tag found = tags.FirstOrDefault(tag => tag.Name == tagName);
        if(found == null) {
            parsed = new ParsedString(fullText);
            nextIndex = closeIndex + 1;
            return true;
        }

        if(isFormat) {
            if(found.FormattingType == Tag.FormatType.None || arguments.Count == 0 || !IsValidFormat(found.FormattingType, arguments[0])) {
                parsed = new ParsedString(fullText);
            } else {
                parsed = new ParsedFormatTag(found, found.FormattingType, arguments[0]);
            }
            nextIndex = closeIndex + 1;
            return true;
        }

        List<Tag> nestedReferences = CollectNestedReferences(arguments, tags, config, found);
        parsed = new ParsedTag(found, arguments, nestedReferences);
        nextIndex = closeIndex + 1;
        return true;
    }

    private static bool IsValidFormat(Tag.FormatType type, string format) {
        try {
            if(type == Tag.FormatType.Float) {
                _ = 123.456f.ToString(format);
            } else if(type == Tag.FormatType.Double) {
                _ = 123.456d.ToString(format);
            } else {
                return false;
            }
            return true;
        } catch {
            return false;
        }
    }

    private static int FindMatchingTagEnd(string source, int openIndex, LexConfig config) {
        int depth = 0;
        char quote = '\0';
        bool escaping = false;

        for(int i = openIndex; i < source.Length; i++) {
            char c = source[i];
            if(escaping) { escaping = false; continue; }
            if(c == '\\') { escaping = true; continue; }
            if(quote != '\0') { if(c == quote) quote = '\0'; continue; }
            if(c == '\'' || c == '"') { quote = c; continue; }
            if(c == config.TagStart) depth++;
            else if(c == config.TagEnd) {
                depth--;
                if(depth == 0) return i;
            }
        }
        return -1;
    }

    private static bool TrySplitTagContent(string content, LexConfig config, out string tagName, out List<string> arguments, out bool isFormat) {
        tagName = string.Empty;
        arguments = [];
        isFormat = false;

        int separator = FindTopLevelSeparator(content, config);
        if(separator < 0) {
            tagName = content.Trim();
            return tagName.Length > 0;
        }

        tagName = content.Substring(0, separator).Trim();
        if(tagName.Length == 0) return false;

        char sep = content[separator];
        if(sep == config.TagOptSeparator) {
            arguments.Add(UnescapeArgument(content.Substring(separator + 1)));
            return true;
        }
        if(sep == ';') {
            isFormat = true;
            arguments.Add(UnescapeArgument(content.Substring(separator + 1)));
            return true;
        }
        if(sep == config.TagArgStart) {
            int argEnd = FindMatchingParen(content, separator, config);
            if(argEnd < 0) return false;
            string argText = content.Substring(separator + 1, argEnd - separator - 1);
            arguments.AddRange(SplitArguments(argText, config));
            return true;
        }
        return false;
    }

    private static int FindTopLevelSeparator(string content, LexConfig config) {
        int braceDepth = 0, parenDepth = 0;
        char quote = '\0';
        bool escaping = false;
        for(int i = 0; i < content.Length; i++) {
            char c = content[i];
            if(escaping) { escaping = false; continue; }
            if(c == '\\') { escaping = true; continue; }
            if(quote != '\0') { if(c == quote) quote = '\0'; continue; }
            if(c == '\'' || c == '"') { quote = c; continue; }
            if(c == config.TagStart) { braceDepth++; continue; }
            if(c == config.TagEnd && braceDepth > 0) { braceDepth--; continue; }
            if(braceDepth > 0) continue;
            if(c == config.TagArgStart) {
                if(parenDepth == 0) return i;
                parenDepth++;
                continue;
            }
            if(c == config.TagArgEnd && parenDepth > 0) { parenDepth--; continue; }
            if((c == config.TagOptSeparator || c == ';') && parenDepth == 0) return i;
        }
        return -1;
    }

    private static int FindMatchingParen(string content, int openIndex, LexConfig config) {
        int parenDepth = 0, braceDepth = 0;
        char quote = '\0';
        bool escaping = false;
        for(int i = openIndex; i < content.Length; i++) {
            char c = content[i];
            if(escaping) { escaping = false; continue; }
            if(c == '\\') { escaping = true; continue; }
            if(quote != '\0') { if(c == quote) quote = '\0'; continue; }
            if(c == '\'' || c == '"') { quote = c; continue; }
            if(c == config.TagStart) { braceDepth++; continue; }
            if(c == config.TagEnd && braceDepth > 0) { braceDepth--; continue; }
            if(braceDepth > 0) continue;
            if(c == config.TagArgStart) { parenDepth++; continue; }
            if(c == config.TagArgEnd) {
                parenDepth--;
                if(parenDepth == 0) return i;
            }
        }
        return -1;
    }

    private static List<string> SplitArguments(string argText, LexConfig config) {
        List<string> result = [];
        StringBuilder current = new();
        int braceDepth = 0, parenDepth = 0;
        char quote = '\0';
        bool escaping = false;
        for(int i = 0; i < argText.Length; i++) {
            char c = argText[i];
            if(escaping) { current.Append(c); escaping = false; continue; }
            if(c == '\\') { escaping = true; continue; }
            if(quote != '\0') { if(c == quote) quote = '\0'; else current.Append(c); continue; }
            if(c == '\'' || c == '"') { quote = c; continue; }
            if(c == config.TagStart) { braceDepth++; current.Append(c); continue; }
            if(c == config.TagEnd && braceDepth > 0) { braceDepth--; current.Append(c); continue; }
            if(braceDepth == 0) {
                if(c == config.TagArgStart) { parenDepth++; current.Append(c); continue; }
                if(c == config.TagArgEnd && parenDepth > 0) { parenDepth--; current.Append(c); continue; }
                if(c == config.TagArgSeparator && parenDepth == 0) {
                    result.Add(current.ToString().Trim());
                    current.Clear();
                    continue;
                }
            }
            current.Append(c);
        }
        result.Add(current.ToString().Trim());
        return result;
    }

    private static string UnescapeArgument(string value) {
        if(string.IsNullOrEmpty(value) || value.IndexOf('\\') < 0) return value;
        StringBuilder sb = new();
        bool escaping = false;
        foreach(char c in value) {
            if(escaping) { sb.Append(c); escaping = false; }
            else if(c == '\\') escaping = true;
            else sb.Append(c);
        }
        if(escaping) sb.Append('\\');
        return sb.ToString();
    }

    private static List<Tag> CollectNestedReferences(List<string> arguments, List<Tag> tags, LexConfig config, Tag owner) {
        List<Tag> result = [];
        foreach(string arg in arguments) {
            foreach(IParsed parsed in ParseText(arg, tags, config)) {
                if(parsed is ParsedTag tag) {
                    foreach(Tag referenced in tag.ReferencedTags) AddReference(result, referenced);
                } else if(parsed is ParsedFormatTag formatTag) {
                    AddReference(result, formatTag.tag);
                }
            }
            if(IsExpressionLikeTag(owner)) {
                foreach(Tag tag in tags) {
                    if(!ReferenceEquals(tag, owner) && ContainsBareTagReference(arg, tag.Name)) AddReference(result, tag);
                }
            }
        }
        return result;
    }

    private static bool IsExpressionLikeTag(Tag tag) {
        string name = tag?.Name ?? string.Empty;
        return name == "Expr" || name == "Calc" || name == "ExprText" || name == "IfExpr" || name == "IfText" || name == "Coalesce" || name == "Expression";
    }
    private static void AddReference(List<Tag> result, Tag tag) { if(tag != null && !result.Contains(tag)) result.Add(tag); }
    private static bool ContainsBareTagReference(string text, string tagName) {
        if(string.IsNullOrEmpty(text) || string.IsNullOrEmpty(tagName)) return false;
        int index = 0;
        while((index = text.IndexOf(tagName, index, StringComparison.OrdinalIgnoreCase)) >= 0) {
            int before = index - 1;
            int after = index + tagName.Length;
            bool beforeOk = before < 0 || !IsIdentifierChar(text[before]);
            bool afterOk = after >= text.Length || !IsIdentifierChar(text[after]) || text[after] == ':';
            if(beforeOk && afterOk) return true;
            index += tagName.Length;
        }
        return false;
    }
    private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static IEnumerable<IParsed> ParseTokensLegacy(IEnumerable<Token> tokens, List<Tag> tags, LexConfig config) {
        Queue<Token> queue = new(tokens);
        while(queue.Count > 0) {
            Token t = queue.Dequeue();
            if(t.type == TokenType.TagStart) {
                if(queue.Count > 0 && queue.Peek().type == TokenType.TagEnd) {
                    queue.Dequeue();
                    yield return new ParsedString(config.TagStart.ToString() + config.TagEnd.ToString());
                    continue;
                }
                Tag found = null;
                StringBuilder sb = new();
                sb.Append(config.TagStart);
                bool tagNotFound = false;
                List<string> arguments = [];
                while(queue.Count > 0 && t.type != TokenType.TagEnd) {
                    t = queue.Dequeue();
                    if(tagNotFound) { sb.Append(t.value); continue; }
                    if(t.type == TokenType.Identifier) {
                        found = tags.FirstOrDefault(tag => tag.Name == t.value);
                        if(tagNotFound = found == null) { sb.Append(t.value); continue; }
                    }
                    if(t.type is TokenType.ArgStart or TokenType.Colon) {
                        while(queue.Count > 0 && t.type != TokenType.ArgEnd && t.type != TokenType.TagEnd) {
                            t = queue.Dequeue();
                            if(t.type == TokenType.Identifier) arguments.Add(t.value);
                        }
                    }
                }
                yield return tagNotFound ? new ParsedString(sb.ToString()) : new ParsedTag(found, arguments);
            } else {
                yield return new ParsedString(t.value);
            }
        }
    }
}
