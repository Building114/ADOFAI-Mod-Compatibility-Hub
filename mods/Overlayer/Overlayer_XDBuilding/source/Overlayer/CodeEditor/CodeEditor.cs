using Overlayer.Core;
using Overlayer.Tags;
using Overlayer.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Overlayer.CodeEditor;

public class CodeEditor {
    public static CodeEditor instance = new("OverlayerCodeEditor", new CodeTheme {
        background = "#333333",
        linenumbg = "#222222",
        color = "#FFFFFF",
        selection = "#264F78",
        cursor = "#D4D4D4"
    });

    public static Regex color = new(@"<color=(#[0-9a-fA-F]{3,8})>", RegexOptions.Compiled);

    private sealed class TagSpan {
        public int Open;
        public int Close;
        public string Body;
        public string Name;
    }

    private static string GetTagName(string body) {
        int separator = body.IndexOfAny(new[] { ':', ';', '(' });
        return (separator < 0 ? body : body.Substring(0, separator)).Trim();
    }

    private static List<TagSpan> FindTags(string source) {
        List<TagSpan> result = [];
        List<int> openings = [];
        char quote = '\0';
        bool escaped = false;
        for(int i = 0; i < source.Length; i++) {
            char c = source[i];
            if(escaped) { escaped = false; continue; }
            if(c == '\\') { escaped = true; continue; }
            if(openings.Count > 0) {
                if(quote != '\0') {
                    if(c == quote) quote = '\0';
                    continue;
                }
                if(c == '\'' || c == '"') { quote = c; continue; }
            }
            if(c == '{') {
                if(openings.Count == 0 && i + 1 < source.Length && source[i + 1] == '{') {
                    i++;
                    continue;
                }
                openings.Add(i);
            } else if(c == '}' && openings.Count > 0) {
                int start = openings[openings.Count - 1];
                openings.RemoveAt(openings.Count - 1);
                string body = source.Substring(start + 1, i - start - 1);
                result.Add(new TagSpan { Open = start, Close = i, Body = body, Name = GetTagName(body) });
            }
        }
        return result;
    }

    private static bool IsEditableTag(string body, string name) =>
        body == name || (body.StartsWith(name + "(", StringComparison.Ordinal) && body.EndsWith(")", StringComparison.Ordinal));

    internal static bool TryGetEditorArguments(string tag, string name, out string[] arguments) {
        arguments = Array.Empty<string>();
        tag = tag?.Trim();
        if(tag == name) return true;
        if(tag == null || !tag.StartsWith(name + "(", StringComparison.Ordinal) || !tag.EndsWith(")", StringComparison.Ordinal)) return false;

        string contents = tag.Substring(name.Length + 1, tag.Length - name.Length - 2);
        if(contents.Length == 0) return true;

        List<string> parsed = [];
        StringBuilder current = new();
        int nestedTags = 0, nestedParens = 0;
        char quote = '\0';
        bool escaped = false;
        foreach(char c in contents) {
            if(escaped) { current.Append(c); escaped = false; continue; }
            if(c == '\\') { current.Append(c); escaped = true; continue; }
            if(quote != '\0') {
                current.Append(c);
                if(c == quote) quote = '\0';
                continue;
            }
            if(c == '\'' || c == '"') { quote = c; current.Append(c); continue; }
            if(c == '{') nestedTags++;
            else if(c == '}') { if(--nestedTags < 0) return false; }
            else if(nestedTags == 0 && c == '(') nestedParens++;
            else if(nestedTags == 0 && c == ')') { if(--nestedParens < 0) return false; }
            if(c == ',' && nestedTags == 0 && nestedParens == 0) {
                parsed.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }
            current.Append(c);
        }
        if(escaped || quote != '\0' || nestedTags != 0 || nestedParens != 0) return false;
        parsed.Add(current.ToString().Trim());
        arguments = parsed.ToArray();
        return true;
    }

    private static string HighlightTags(string source) {
        source ??= string.Empty;
        Dictionary<int, string> opens = [];
        Dictionary<int, string> closes = [];
        foreach(TagSpan span in FindTags(source)) {
            string colorName = TagManager.tags != null && TagManager.tags.ContainsKey(span.Name)
                ? (span.Body.Substring(span.Name.Length).TrimStart().StartsWith(";", StringComparison.Ordinal) ? "blue" :
                    ((span.Name == nameof(Effect.ColorRange) && Main.Settings.ColorRangeEditor) ||
                     (span.Name == nameof(Effect.MovingMan) && Main.Settings.MovingManEditor) ||
                     (span.Name == nameof(Effect.EasedValue) && Main.Settings.EasedValueEditor)) ? "orange" : "lightblue")
                : "red";
            opens[span.Open] = "<color=" + colorName + ">";
            closes[span.Close] = "</color>";
        }
        foreach(Match match in color.Matches(source)) {
            Group code = match.Groups[1];
            if(code.Success && ColorUtility.TryParseHtmlString(code.Value, out _)) {
                opens[code.Index] = "<color=" + code.Value + ">";
                closes[code.Index + code.Length - 1] = "</color>";
            }
        }
        StringBuilder output = new(source.Length * 2);
        for(int i = 0; i < source.Length; i++) {
            if(opens.TryGetValue(i, out string prefix)) output.Append(prefix);
            output.Append(source[i] == '<' ? "<<b></b>" : source[i].ToString());
            if(closes.TryGetValue(i, out string suffix)) output.Append(suffix);
        }
        return output.ToString();
    }

    public static void Initialize() {
        instance.highlighter = HighlightTags;
    }

    public string controlName { get; set; }
    public System.Action onValueChange;
    public int tabSpaces = 2;
    public System.Func<string, string> highlighter { get; set; }

    private string cachedCode { get; set; }
    private string cachedHighlightedCode { get; set; }

    private CodeTheme theme;

    private int charWidth = 11;
    private bool pressedTab = false;
    private bool pressedShift = false;

    private MovingManEditor movingManEditor;
    private ColorRangeEditor colorRangeEditor;
    private EasedValueEditor easedValueEditor;
    private int editingHash;


    public bool isFocused => GUI.GetNameOfFocusedControl() == controlName;

    public CodeEditor(string controlName, CodeTheme theme) {
        this.controlName = controlName;
        this.theme = theme;
        highlighter = code => code;
    }

    private string selectedtag = nameof(Developers.Developer);

    internal Dictionary<string, UndoRedoManager> undoRedoManagers = [];

    public string Draw(string code, GUIStyle style, string id, params GUILayoutOption[] options) {
        code ??= string.Empty;
        if(!undoRedoManagers.ContainsKey(id)) {
            undoRedoManagers[id] = new UndoRedoManager();
            undoRedoManagers[id].SaveState(code);
        }

        controlName = id;
        var oldEvent = new Event(Event.current);
        if(movingManEditor) {
            if(movingManEditor.HasChanges && editingHash == code.GetHashCode()) {
                code = movingManEditor.codesBefore + nameof(Effect.MovingMan) + "(" + movingManEditor.targetTag + "," +
                       movingManEditor.startSize.ToString(CultureInfo.InvariantCulture) + "," + movingManEditor.endSize.ToString(CultureInfo.InvariantCulture) + "," +
                       movingManEditor.defaultSize.ToString(CultureInfo.InvariantCulture) + "," + movingManEditor.speed.ToString(CultureInfo.InvariantCulture) + "," +
                       movingManEditor.invert + "," + movingManEditor.ease + ")" + movingManEditor.codesAfter;
                editingHash = code.GetHashCode();
            }
        }

        if(colorRangeEditor) {
            if(colorRangeEditor.HasChanges && editingHash == code.GetHashCode()) {
                code = colorRangeEditor.codesBefore + nameof(Effect.ColorRange) + "(" + colorRangeEditor.targetTag + "," +
                       colorRangeEditor.valueMin.ToString(CultureInfo.InvariantCulture) + "," + colorRangeEditor.valueMax.ToString(CultureInfo.InvariantCulture) + "," +
                       ColorUtility.ToHtmlStringRGBA(colorRangeEditor.colorMin) + "," +
                       ColorUtility.ToHtmlStringRGBA(colorRangeEditor.colorMax) + "," +
                       colorRangeEditor.ease + "," + colorRangeEditor.maxLength +
                       ")" + colorRangeEditor.codesAfter;
                editingHash = code.GetHashCode();
            }
        }

        if(easedValueEditor) {
            if(easedValueEditor.HasChanges && editingHash == code.GetHashCode()) {
                code = easedValueEditor.codesBefore + nameof(Effect.EasedValue) + "(" + easedValueEditor.targetTag + "," +
                       easedValueEditor.digits + "," + easedValueEditor.speed.ToString(CultureInfo.InvariantCulture) + "," +
                       easedValueEditor.ease + ")" + easedValueEditor.codesAfter;
                editingHash = code.GetHashCode();
            }
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label(Drawer.Icon_Parse);
        GUILayout.Space(2);
        Drawer.DrawTags(ref selectedtag);

        if(Drawer.Button(Main.Lang.Get("INSERT", "Insert"))) {
            TextEditor editor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
            var sb = new StringBuilder(code);
            int cursor = editor == null ? code.Length : Math.Max(0, Math.Min(code.Length, editor.selectIndex));
            sb.Insert(cursor, "{" + selectedtag + "}");
            code = sb.ToString();
            undoRedoManagers[id].SaveState(code);
            onValueChange?.Invoke();
        }

        GUILayout.FlexibleSpace();

        GUILayout.EndHorizontal();
        float lineCountWidth = code.Split('\n').Length.ToString().Length * charWidth;
        var preBackgroundColor = GUI.backgroundColor;
        var preColor = GUI.color;
        Color preSelection = GUI.skin.settings.selectionColor;
        Color preCursor = GUI.skin.settings.cursorColor;
        float preFlashSpeed = GUI.skin.settings.cursorFlashSpeed;

        GUI.backgroundColor = GetColor(theme.background);
        GUI.color = GetColor(theme.color);
        GUI.skin.settings.selectionColor = GetColor(theme.selection);
        GUI.skin.settings.cursorColor = GetColor(theme.cursor);
        GUI.skin.settings.cursorFlashSpeed = 0;

        var backStyle = new GUIStyle(style) {
            richText = false
        };
        backStyle.normal.textColor = Color.clear;
        backStyle.hover.textColor = Color.clear;
        backStyle.active.textColor = Color.clear;
        backStyle.focused.textColor = Color.clear;

        backStyle.normal.background = Texture2D.whiteTexture;
        backStyle.hover.background = Texture2D.whiteTexture;
        backStyle.active.background = Texture2D.whiteTexture;
        backStyle.focused.background = Texture2D.whiteTexture;

        backStyle.padding.left = 5;

        GUILayout.BeginHorizontal();

        // Line numbers
        DrawLineNumbers(code, style);

        // Handle tab key
        bool usedTab = Event.current.type != EventType.Layout
                       && (Event.current.keyCode == KeyCode.Tab || Event.current.character == '\t');

        pressedTab = usedTab && Event.current.type == EventType.KeyDown;
        pressedShift = Event.current.shift;

        if(usedTab) {
            Event.current.Use();
        }

        // Drawing the text area using GUILayout
        GUI.SetNextControlName(controlName);
        var editorw = 700;

        if(isFocused) {
            if(Event.current.type == EventType.KeyDown) {
                var oldcode = code;
                if(Event.current.keyCode == KeyCode.Z && Event.current.control) {
                    if(Event.current.shift) {
                        var tx = undoRedoManagers[id].Redo();
                        if(tx != null) {
                            code = tx;
                        }
                    } else {
                        var tx = undoRedoManagers[id].Undo();
                        if(tx != null) {
                            code = tx;
                        }
                    }
                } else if((Event.current.keyCode == KeyCode.Y && Event.current.control) || (Event.current.shift && Event.current.keyCode == KeyCode.Z)) {
                    var tx = undoRedoManagers[id].Redo();
                    if(tx != null) {
                        code = tx;
                    }
                }

                if(code != oldcode) {
                    Event.current.Use();
                }
            }
        }

        if(!movingManEditor && !colorRangeEditor && !easedValueEditor) {
            GUI.SetNextControlName(id);
            string editedCode = GUILayout.TextArea(code, backStyle, GUILayout.ExpandHeight(true),
                GUILayout.Width(Math.Max(editorw, style.CalcSize(new GUIContent(code)).x + 5)));
            if(editedCode != code) {
                code = editedCode;
                undoRedoManagers[id].SaveState(code);
                onValueChange?.Invoke();
            }
        } else {
            GUILayout.Box(code, backStyle, GUILayout.ExpandHeight(true),
                GUILayout.Width(Math.Max(editorw, style.CalcSize(new GUIContent(code)).x + 5)));
        }

        if(cachedCode != code) {
            cachedCode = code;
            cachedHighlightedCode = highlighter(code);
        }

        // Render syntax highlighting
        GUI.backgroundColor = Color.clear;

        var foreStyle = new GUIStyle(style) {
            richText = true
        };

        foreStyle.normal.textColor = GUI.color;
        foreStyle.hover.textColor = GUI.color;
        foreStyle.active.textColor = GUI.color;
        foreStyle.focused.textColor = GUI.color;
        foreStyle.padding.left = 5;

        // Render highlighted text
        GUI.Label(GUILayoutUtility.GetLastRect(), cachedHighlightedCode, foreStyle);

        var bak = Event.current;
        Event.current = oldEvent;

        if(!movingManEditor && !colorRangeEditor && !easedValueEditor) {
            // Get Tags
            foreach(TagSpan match in FindTags(code)) {
                var tag = match.Body;
                var start = match.Open + 1;
                var end = match.Close;
                var codesBefore = code.Substring(0, start);
                var codesAfter = code.Substring(end, code.Length - end);
                var lines = codesBefore.Split('\n');
                var lastline = lines[lines.Length - 1];
                var height = style.lineHeight;

                var len = lines.Length - 1;
                /*var xc = 0f;
                foreach (var l in lines)
                {
                    xc = style.CalcSize(new GUIContent(l)).x;
                    while (xc >= editorw - 5)
                    {
                        xc -= editorw - 5;
                        len++;
                    }
                }*/

                var width = style.CalcSize(new GUIContent(lastline)).x;

                /*while (width >= editorw - 5)
                {
                    width -= editorw - 5;
                }*/

                var y = len * height;

                var x = width + 5;

                var rect = GUILayoutUtility.GetLastRect();
                rect.x += x;
                rect.y += y;

                rect.width = style.CalcSize(new GUIContent(tag)).x;

                rect.height = height;

                var mvm = match.Name == nameof(Effect.MovingMan) && IsEditableTag(tag, match.Name);

                var cr = match.Name == nameof(Effect.ColorRange) && IsEditableTag(tag, match.Name);

                var ev = match.Name == nameof(Effect.EasedValue) && IsEditableTag(tag, match.Name);

                var special = mvm || cr || ev;

                if(mvm && !Main.Settings.MovingManEditor) {
                    special = false;
                }

                if(cr && !Main.Settings.ColorRangeEditor) {
                    special = false;
                }

                if(ev && !Main.Settings.EasedValueEditor) {
                    special = false;
                }

                if(rect.Contains(Event.current.mousePosition)) {
                    var pars = match.Name;
                    Main.tooltip = TagManager.tags != null && TagManager.tags.ContainsKey(pars)
                                   ? Main.Lang.Get($"TAG_DESC_{pars.ToUpper()}", TagDesc.GetTagDesc(pars))
                                   : Main.Lang.Get("NOT_EXIST_TAG", "This tag does not exist");
                }

                if(special) {
                    if(GUI.Button(rect, "")) {
                        bool opened = false;
                        if(cr) {
                            var editor = new GameObject("Overlayer.ColorRangeEditor").AddComponent<ColorRangeEditor>();
                            if(editor.Initialize(tag, codesBefore, codesAfter)) {
                                colorRangeEditor = editor;
                                Object.DontDestroyOnLoad(editor.gameObject);
                                opened = true;
                            } else Object.Destroy(editor.gameObject);
                        } else if(mvm) {
                            var editor = new GameObject("Overlayer.MovingManEditor").AddComponent<MovingManEditor>();
                            if(editor.Initialize(tag, codesBefore, codesAfter)) {
                                movingManEditor = editor;
                                Object.DontDestroyOnLoad(editor.gameObject);
                                opened = true;
                            } else Object.Destroy(editor.gameObject);
                        } else if(ev) {
                            var editor = new GameObject("Overlayer.EasedValueEditor").AddComponent<EasedValueEditor>();
                            if(editor.Initialize(tag, codesBefore, codesAfter)) {
                                easedValueEditor = editor;
                                Object.DontDestroyOnLoad(editor.gameObject);
                                opened = true;
                            } else Object.Destroy(editor.gameObject);
                        }
                        if(opened) editingHash = code.GetHashCode();
                        else Main.tooltip = "Invalid tag parameters. Edit this tag as text.";
                    }
                }
            }
        }

        Event.current = bak;

        GUI.backgroundColor = preBackgroundColor;
        GUI.color = preColor;
        GUI.skin.settings.selectionColor = preSelection;
        GUI.skin.settings.cursorColor = preCursor;
        GUI.skin.settings.cursorFlashSpeed = preFlashSpeed;

        GUILayout.EndHorizontal();

        return code;
    }

    private string UpdateEditorTabs(string content, bool shift) {
        // Custom tab handling logic for runtime
        string tabrep = new(' ', tabSpaces);

        // Normal case
        if(!shift) {
            content += tabrep;
        } else if(content.Length >= tabSpaces) {
            content = content.Remove(content.Length - tabSpaces, tabSpaces);
        }

        return content;
    }

    private void DrawLineNumbers(string code, GUIStyle baseStyle) {
        float lineCountWidth = code.Split('\n').Length.ToString().Length * charWidth;

        // Reserve space
        Rect rect = GUILayoutUtility.GetRect(lineCountWidth, 100, GUILayout.ExpandHeight(true),
            GUILayout.ExpandWidth(false));

        string lineString = "";
        var i = 0;
        float curwidth;
        foreach(var st in code.Split('\n')) {
            curwidth = 0;
            lineString += ++i + "\n";
            foreach(var ch in st) {
                curwidth += baseStyle.CalcSize(new GUIContent(ch.ToString())).x;
                /*if (curwidth >= editorw - 5)
                {
                    lineString += "\n";
                    curwidth -= editorw - 5;
                }*/
            }
        }

        GUIStyle style = new(baseStyle);
        style.normal.textColor = Color.white;

        style.normal.background = Texture2D.whiteTexture;
        style.hover.background = Texture2D.whiteTexture;
        style.active.background = Texture2D.whiteTexture;
        style.focused.background = Texture2D.whiteTexture;

        style.alignment = TextAnchor.UpperCenter;

        GUI.backgroundColor = GetColor(theme.linenumbg);

        GUI.Label(rect, new GUIContent(lineString), style);

        GUI.backgroundColor = GetColor(theme.background);
    }

    private Color GetColor(string colorCode) {
        Color color = Color.magenta;
        ColorUtility.TryParseHtmlString(colorCode, out color);
        return color;
    }
}