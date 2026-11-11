using DG.Tweening;
using Overlayer.Core;
using Overlayer.Patches;
using Overlayer.Tags;
using Overlayer.Utils;
using RapidGUI;
using System;
using System.Globalization;
using UnityEngine;

namespace Overlayer.CodeEditor;

internal class ColorRangeEditor : MonoBehaviour {
    public string codesBefore;
    public string codesAfter;
    private Rect windowRect;
    private Rect previewWindowRect;
    private bool isInitaialize = false;
    private bool isSpawn = false;
    private float testvalue;

    public string targetTag = nameof(ComboStats.Combo);
    public double valueMin = 0;
    public double valueMax = 100;
    public Color colorMin = Color.black;
    public Color colorMax = Color.white;
    public Ease ease = Ease.OutExpo;
    public int maxLength = -1;

    private NeoDrawer neoDrawer;
    public bool HasChanges { get; private set; }

    public bool Initialize(string tag, string codesBefore, string codesAfter) {
        if(!CodeEditor.TryGetEditorArguments(tag, nameof(Effect.ColorRange), out string[] arr)) return false;
        if(arr.Length > 7 || (arr.Length != 0 && arr.Length < 5)) return false;
        if(arr.Length > 0) {
            if(string.IsNullOrWhiteSpace(arr[0]) || arr[0].Contains("{") ||
               TagManager.tags == null || !TagManager.tags.ContainsKey(arr[0])) return false;
            if(!double.TryParse(arr[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double min) ||
               !double.TryParse(arr[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double max) ||
               double.IsNaN(min) || double.IsInfinity(min) || double.IsNaN(max) || double.IsInfinity(max)) return false;
            if(!ColorUtility.TryParseHtmlString(arr[3].StartsWith("#") ? arr[3] : "#" + arr[3], out Color first) ||
               !ColorUtility.TryParseHtmlString(arr[4].StartsWith("#") ? arr[4] : "#" + arr[4], out Color second)) return false;
            Ease parsedEase = ease;
            if(arr.Length > 5 && (!Enum.TryParse(arr[5], true, out parsedEase) || !Enum.IsDefined(typeof(Ease), parsedEase))) return false;
            int parsedLength = maxLength;
            if(arr.Length > 6 && !int.TryParse(arr[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedLength)) return false;
            targetTag = arr[0];
            valueMin = min;
            valueMax = max;
            colorMin = first;
            colorMax = second;
            ease = parsedEase;
            maxLength = parsedLength;
        }
        testvalue = (float)valueMax;
        windowRect.width = 400;
        isInitaialize = true;
        this.codesBefore = codesBefore;
        this.codesAfter = codesAfter;
        BlockUMMClosing.Block = true;
        neoDrawer = new NeoDrawer();
        return true;
    }

    public void OnDestroy() => BlockUMMClosing.Block = false;

    public void OnGUI() {
        if(isInitaialize) {
            var fmt = string.Format(Main.Lang.Get("THIS_EDITOR", "{0} Editor"), nameof(Effect.ColorRange));

            if(!isSpawn && Event.current.type == EventType.Repaint) {
                windowRect = GUILayout.Window(123, windowRect, DrawWindow, fmt, RGUIStyle.darkWindow);
                windowRect.x = (int)((Screen.width * 0.5f) - (windowRect.width * 0.5f));
                windowRect.y = (int)((Screen.height * 0.5f) - (windowRect.height * 0.5f));

                isSpawn = true;
            }

            windowRect = GUILayout.Window(123, windowRect, DrawWindow, fmt, RGUIStyle.darkWindow);

            //var sz = GUI.skin.label.CalcSize(new GUIContent("<size=40>Test</size>"));
            previewWindowRect.x = windowRect.x + windowRect.width + 10;
            previewWindowRect.y = windowRect.y;
            previewWindowRect.width = 280;
            previewWindowRect.height = 150;
            previewWindowRect = GUI.Window(1123, previewWindowRect, PreviewWindow, "",
                RGUIStyle.darkWindow);
        }
    }

    private void PreviewWindow(int windowID) {
        GUI.BringWindowToFront(windowID);
        if(double.IsNaN(valueMin) || double.IsInfinity(valueMin) ||
           double.IsNaN(valueMax) || double.IsInfinity(valueMax) ||
           valueMin >= valueMax || valueMin < -float.MaxValue || valueMax > float.MaxValue ||
           (float)valueMin >= (float)valueMax) {
            GUILayout.Label("Invalid range");
            return;
        }
        testvalue = Mathf.Clamp(testvalue, (float)valueMin, (float)valueMax);
        TagManager.testerValue = testvalue.ToString(CultureInfo.InvariantCulture);
        var col = Effect.ColorRange("INTERNAL_TESTER_TAG_1234512345_" + targetTag, valueMin, valueMax,
            ColorUtility.ToHtmlStringRGBA(colorMin), ColorUtility.ToHtmlStringRGBA(colorMax), ease, maxLength);
        neoDrawer.DrawSingleWithSlider("Value", ref testvalue, (float)valueMin, (float)valueMax, 100, "Pre");
        if(col.Length == 8 || col.Length == 6) GUILayout.Label("<size=40><color=#" + col + ">Test</color></size>");
        else GUILayout.Label("Invalid color output");
    }

    private void DrawWindow(int windowID) {
        neoDrawer.FieldResetId();
        string oldTarget = targetTag;
        double oldMin = valueMin, oldMax = valueMax;
        Color oldColorMin = colorMin, oldColorMax = colorMax;
        Ease oldEase = ease;
        int oldLength = maxLength;

        GUI.BringWindowToFront(windowID);
        GUILayout.BeginVertical();
        GUILayout.Space(10);

        GUILayout.BeginHorizontal();
        GUILayout.Label(Main.Lang.Get("TARGET_TAG", "Target Tag"));
        Drawer.DrawTags(ref targetTag);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        neoDrawer.DrawDouble(Main.Lang.Get("VALUE_MIN", "Min Value"), ref valueMin);
        neoDrawer.DrawDouble(Main.Lang.Get("VALUE_MAX", "Max Value"), ref valueMax);
        neoDrawer.DrawColor(Main.Lang.Get("COLOR_MIN", "Min Color"), ref colorMin, 180f);
        neoDrawer.DrawColor(Main.Lang.Get("COLOR_MAX", "Max Color"), ref colorMax, 180f);

        GUILayout.BeginHorizontal();
        GUILayout.Label(Main.Lang.Get("EASE", "Ease"));
        Drawer.DrawEase(ref ease);

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        neoDrawer.DrawInt32("maxLength", ref maxLength);

        neoDrawer.UpdateFocused();
        if(oldTarget != targetTag || oldMin != valueMin || oldMax != valueMax ||
           oldColorMin != colorMin || oldColorMax != colorMax || oldEase != ease || oldLength != maxLength) HasChanges = true;

        if(Drawer.Button(Main.Lang.Get("DONE", "Done"))) {
            neoDrawer = null;
            BlockUMMClosing.Block = false;
            Destroy(gameObject);
        }

        GUILayout.Space(10);
        GUILayout.EndVertical();
    }
}