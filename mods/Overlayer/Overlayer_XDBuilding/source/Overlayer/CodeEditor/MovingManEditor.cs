using DG.Tweening;
using Overlayer.Core;
using Overlayer.Patches;
using Overlayer.Tags;
using Overlayer.Utils;
using RapidGUI;
using System;
using System.Globalization;
using UnityEngine;
using Time = UnityEngine.Time;

namespace Overlayer.CodeEditor;

internal class MovingManEditor : MonoBehaviour {
    public string codesBefore;
    public string codesAfter;
    private Rect windowRect;
    private Rect previewWindowRect;
    private bool isInitaialize = false;
    private bool isSpawn = false;

    private int tester = 0;
    private float timer = 0;

    public string targetTag = nameof(ComboStats.Combo);
    public double startSize = 30;

    public double endSize = 80;
    public double defaultSize = 30;
    public double speed = 800;

    public bool invert = false;
    public Ease ease = Ease.OutExpo;

    private NeoDrawer neoDrawer;
    public bool HasChanges { get; private set; }

    public bool Initialize(string tag, string codesBefore, string codesAfter) {
        if(!CodeEditor.TryGetEditorArguments(tag, nameof(Effect.MovingMan), out string[] arr) || arr.Length > 7) return false;
        if(arr.Length > 0) {
            if(string.IsNullOrWhiteSpace(arr[0]) || arr[0].Contains("{") ||
               TagManager.tags == null || !TagManager.tags.ContainsKey(arr[0])) return false;
            double parsedStart = startSize, parsedEnd = endSize, parsedDefault = defaultSize, parsedSpeed = speed;
            bool parsedInvert = invert;
            Ease parsedEase = ease;
            if(arr.Length > 1 && !double.TryParse(arr[1], NumberStyles.Float, CultureInfo.InvariantCulture, out parsedStart)) return false;
            if(arr.Length > 2 && !double.TryParse(arr[2], NumberStyles.Float, CultureInfo.InvariantCulture, out parsedEnd)) return false;
            if(arr.Length > 3 && !double.TryParse(arr[3], NumberStyles.Float, CultureInfo.InvariantCulture, out parsedDefault)) return false;
            if(arr.Length > 4 && !double.TryParse(arr[4], NumberStyles.Float, CultureInfo.InvariantCulture, out parsedSpeed)) return false;
            if(arr.Length > 5 && !bool.TryParse(arr[5], out parsedInvert)) return false;
            if(arr.Length > 6 && (!Enum.TryParse(arr[6], true, out parsedEase) || !Enum.IsDefined(typeof(Ease), parsedEase))) return false;
            if(double.IsNaN(parsedStart) || double.IsInfinity(parsedStart) ||
               double.IsNaN(parsedEnd) || double.IsInfinity(parsedEnd) ||
               double.IsNaN(parsedDefault) || double.IsInfinity(parsedDefault) ||
               double.IsNaN(parsedSpeed) || double.IsInfinity(parsedSpeed)) return false;
            targetTag = arr[0];
            startSize = parsedStart;
            endSize = parsedEnd;
            defaultSize = parsedDefault;
            speed = parsedSpeed;
            invert = parsedInvert;
            ease = parsedEase;
        }
        windowRect.width = 300;
        isInitaialize = true;
        this.codesBefore = codesBefore;
        this.codesAfter = codesAfter;
        BlockUMMClosing.Block = true;
        TagManager.testerValue = "0";
        neoDrawer = new NeoDrawer();
        return true;
    }

    public void OnDestroy() => BlockUMMClosing.Block = false;

    public void Update() {
        if(speed <= 0 || double.IsNaN(speed) || double.IsInfinity(speed)) return;
        timer += Time.deltaTime;
        if(timer >= speed / 1000f) {
            timer -= (float)speed / 1000f;
            tester++;
            if(tester > 100) {
                tester = 0;
            }

            TagManager.testerValue = tester.ToString();
        }
    }

    public void OnGUI() {
        if(isInitaialize) {
            var fmt = string.Format(Main.Lang.Get("THIS_EDITOR", "{0} Editor"), nameof(Effect.MovingMan));
            if(!isSpawn && Event.current.type == EventType.Repaint) {
                windowRect = GUILayout.Window(122, windowRect, DrawWindow, fmt, RGUIStyle.darkWindow);
                windowRect.x = (int)((Screen.width * 0.5f) - (windowRect.width * 0.5f));
                windowRect.y = (int)((Screen.height * 0.5f) - (windowRect.height * 0.5f));

                isSpawn = true;
            }

            windowRect = GUILayout.Window(122, windowRect, DrawWindow, fmt, RGUIStyle.darkWindow);
            var size = Math.Max(Math.Max(startSize / 2, endSize / 2), defaultSize / 2);
            size = double.IsNaN(size) || double.IsInfinity(size) ? 20 : Math.Max(1, Math.Min(300, size));
            var txt = "<size=" + size.ToString(CultureInfo.InvariantCulture) + ">Test</size>";
            var sz = GUI.skin.label.CalcSize(new GUIContent(txt));
            previewWindowRect.width = sz.x + 50;
            previewWindowRect.height = sz.y + 50;
            previewWindowRect.x = windowRect.x + windowRect.width + 10;
            previewWindowRect.y = windowRect.y;
            previewWindowRect = GUI.Window(1122, previewWindowRect, PreviewWindow, "",
                RGUIStyle.darkWindow);
        }
    }

    private void PreviewWindow(int windowID) {
        GUI.BringWindowToFront(windowID);
        double size = speed > 0 ? Effect.MovingMan("INTERNAL_TESTER_TAG_1234512345", startSize, endSize,
            defaultSize, speed, invert, ease) / 2 : defaultSize / 2;
        if(double.IsNaN(size) || double.IsInfinity(size)) size = 20;
        GUILayout.Label("<size=" + Math.Max(1, Math.Min(300, size)).ToString(CultureInfo.InvariantCulture) + ">Test</size>");
    }

    private void DrawWindow(int windowID) {
        neoDrawer.FieldResetId();
        string oldTarget = targetTag;
        double oldStart = startSize, oldEnd = endSize, oldDefault = defaultSize, oldSpeed = speed;
        bool oldInvert = invert;
        Ease oldEase = ease;

        GUI.BringWindowToFront(windowID);

        GUILayout.BeginVertical();
        GUILayout.Space(10);

        GUILayout.BeginHorizontal();
        GUILayout.Label(Main.Lang.Get("TARGET_TAG", "Target Tag"));
        Drawer.DrawTags(ref targetTag);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        neoDrawer.DrawDouble(Main.Lang.Get("START_SIZE", "Start Size"), ref startSize);
        neoDrawer.DrawDouble(Main.Lang.Get("END_SIZE", "End Size"), ref endSize);
        neoDrawer.DrawDouble(Main.Lang.Get("DEFAULT_SIZE", "Default Size"), ref defaultSize);
        neoDrawer.DrawDouble(Main.Lang.Get("SPEED", "Speed"), ref speed);

        Drawer.DrawBool(Main.Lang.Get("INVERT", "Invert"), ref invert);
        GUILayout.BeginHorizontal();
        GUILayout.Label(Main.Lang.Get("EASE", "Ease"));
        Drawer.DrawEase(ref ease);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        neoDrawer.UpdateFocused();
        if(oldTarget != targetTag || oldStart != startSize || oldEnd != endSize ||
           oldDefault != defaultSize || oldSpeed != speed || oldInvert != invert || oldEase != ease) HasChanges = true;

        if(Drawer.Button(Main.Lang.Get("DONE", "Done"))) {
            neoDrawer = null;
            BlockUMMClosing.Block = false;
            Destroy(gameObject);
        }

        GUILayout.Space(10);
        GUILayout.EndVertical();
    }
}