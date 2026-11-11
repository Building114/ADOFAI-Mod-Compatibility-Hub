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

internal class EasedValueEditor : MonoBehaviour {
    public string codesBefore;
    public string codesAfter;
    private Rect windowRect;
    private Rect previewWindowRect;
    private bool isInitaialize = false;
    private bool isSpawn = false;
    private float testvalue;

    public string targetTag = nameof(AccuracyStats.XAccuracy);
    public int digits = 6;
    public double speed = 1000;

    public Ease ease = Ease.OutQuad;

    private NeoDrawer neoDrawer;
    public bool HasChanges { get; private set; }
    public bool Initialize(string tag, string codesBefore, string codesAfter) {
        if(!CodeEditor.TryGetEditorArguments(tag, nameof(Effect.EasedValue), out string[] arr) || arr.Length > 4) return false;
        if(arr.Length > 0) {
            if(string.IsNullOrWhiteSpace(arr[0]) || arr[0].Contains("{") ||
               TagManager.tags == null || !TagManager.tags.ContainsKey(arr[0])) return false;
            int parsedDigits = digits;
            double parsedSpeed = speed;
            Ease parsedEase = ease;
            if(arr.Length > 1 && !int.TryParse(arr[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedDigits)) return false;
            if(arr.Length > 2 && (!double.TryParse(arr[2], NumberStyles.Float, CultureInfo.InvariantCulture, out parsedSpeed) ||
                                  double.IsNaN(parsedSpeed) || double.IsInfinity(parsedSpeed))) return false;
            if(arr.Length > 3 && (!Enum.TryParse(arr[3], true, out parsedEase) || !Enum.IsDefined(typeof(Ease), parsedEase))) return false;
            targetTag = arr[0];
            digits = parsedDigits;
            speed = parsedSpeed;
            ease = parsedEase;
        }
        windowRect.width = 300;
        isInitaialize = true;
        this.codesBefore = codesBefore;
        this.codesAfter = codesAfter;
        BlockUMMClosing.Block = true;
        TagManager.testerValue = "100";
        neoDrawer = new NeoDrawer();
        return true;
    }

    public void OnDestroy() => BlockUMMClosing.Block = false;

    public void Update() => TagManager.testerValue = testvalue.ToString(CultureInfo.InvariantCulture);

    public void OnGUI() {
        if(isInitaialize) {
            var fmt = string.Format(Main.Lang.Get("THIS_EDITOR", "{0} Editor"), nameof(Effect.EasedValue));
            if(!isSpawn && Event.current.type == EventType.Repaint) {
                windowRect = GUILayout.Window(124, windowRect, DrawWindow, fmt, RGUIStyle.darkWindow);
                windowRect.x = (int)((Screen.width * 0.5f) - (windowRect.width * 0.5f));
                windowRect.y = (int)((Screen.height * 0.5f) - (windowRect.height * 0.5f));

                isSpawn = true;
            }

            windowRect = GUILayout.Window(124, windowRect, DrawWindow, fmt, RGUIStyle.darkWindow);
            previewWindowRect.x = windowRect.x + windowRect.width + 10;
            previewWindowRect.y = windowRect.y;
            previewWindowRect.width = 320;
            previewWindowRect.height = 150;
            previewWindowRect = GUI.Window(1124, previewWindowRect, PreviewWindow, "",
                RGUIStyle.darkWindow);
        }
    }

    private void PreviewWindow(int windowID) {
        GUI.BringWindowToFront(windowID);
        GUILayout.Label("<size=40>" + (speed > 0 && digits <= 15 ? Effect.EasedValue("INTERNAL_TESTER_TAG_1234512345", digits, speed, ease).ToString(CultureInfo.InvariantCulture) : "Invalid speed or digits") + "</size>");
        neoDrawer.DrawSingleWithSlider("Value", ref testvalue, 0, 100, 100, "testvalue");
    }

    private void DrawWindow(int windowID) {
        neoDrawer.FieldResetId();
        string oldTarget = targetTag;
        int oldDigits = digits;
        double oldSpeed = speed;
        Ease oldEase = ease;

        GUI.BringWindowToFront(windowID);

        GUILayout.BeginVertical();
        GUILayout.Space(10);

        GUILayout.BeginHorizontal();
        GUILayout.Label(Main.Lang.Get("TARGET_TAG", "Target Tag"));
        Drawer.DrawTags(ref targetTag);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        neoDrawer.DrawInt32(Main.Lang.Get("DIGITS", "Digits"), ref digits);
        neoDrawer.DrawDouble(Main.Lang.Get("SPEED", "Speed"), ref speed);
        GUILayout.BeginHorizontal();
        GUILayout.Label(Main.Lang.Get("EASE", "Ease"));
        Drawer.DrawEase(ref ease);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        neoDrawer.UpdateFocused();
        if(oldTarget != targetTag || oldDigits != digits || oldSpeed != speed || oldEase != ease) HasChanges = true;

        if(Drawer.Button(Main.Lang.Get("DONE", "Done"))) {
            neoDrawer = null;
            BlockUMMClosing.Block = false;
            Destroy(gameObject);
        }

        GUILayout.Space(10);
        GUILayout.EndVertical();
    }
}
