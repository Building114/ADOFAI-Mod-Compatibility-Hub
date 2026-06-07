using Overlayer.Core;
using Overlayer.Unity;
using RapidGUI;
using System;
using UnityEngine;

namespace Overlayer.Utils;

internal class DeletePopup : MonoBehaviour {

    private Rect windowRect;
    private string[] contentLines;

    private bool isInitaialize = false;
    private bool isAnimating = false;
    private bool isSpawn = false;

    private OverlayerObject obj;
    private OverlayerProfile profile;

    private Action OnDelete;
    private bool isClosing = false;

    public void Initialize(OverlayerObject obj, Action onDelete = null) {
        this.obj = obj;
        this.profile = null;
        this.OnDelete = onDelete;

        contentLines = new[] {
            "<size=30>" + Main.Lang.Get("DESTROY_ASK", "Destroy?") + "</size>\n",
            "<size=20>" + SafeObjectName(obj) + "</size>\n"
        };

        SetupWindow();
    }

    public void Initialize(OverlayerProfile profile, Action onDelete = null) {

        this.profile = profile;
        this.obj = null;
        this.OnDelete = onDelete;

        contentLines =
        [
            "<size=30>" + Main.Lang.Get("DESTROY_ASK", "Destroy?") + "</size>\n",
            "<size=20>" + SafeProfileName(profile) + "</size>\n"
        ];

        SetupWindow();
    }

    private void SetupWindow() {

        float maxWidth = 0f;

        foreach(var line in contentLines) {
            float lineWidth = GUI.skin.label.CalcSize(new GUIContent(line)).x;
            if(lineWidth > maxWidth) {
                maxWidth = lineWidth;
            }
        }

        float width = maxWidth + 40;
        float height = (contentLines.Length * 20) + 40;

        windowRect = new Rect(
            (Screen.width - width) / 2f,
            (Screen.height - height) / 2f,
            width,
            height
        );

        isInitaialize = true;
    }

    private void OnGUI() {

        if(isClosing || !isInitaialize) {
            return;
        }

        if(!isSpawn && Event.current.type == EventType.Repaint) {

            windowRect = GUILayout.Window(
                121,
                windowRect,
                DrawWindow,
                "",
                RGUIStyle.darkWindow,
                GUILayout.MaxWidth(1000)
            );

            windowRect.x = (int)((Screen.width * 0.5f) - (windowRect.width * 0.5f));
            windowRect.y = (int)((Screen.height * 0.5f) - (windowRect.height * 0.5f));

            isSpawn = true;
        }

        windowRect = GUILayout.Window(
            121,
            windowRect,
            DrawWindow,
            "",
            RGUIStyle.darkWindow,
            GUILayout.MaxWidth(1000)
        );
    }

    private void DrawWindow(int windowID) {

        GUI.BringWindowToFront(windowID);

        GUILayout.BeginVertical();
        GUILayout.Space(10);

        GUILayout.FlexibleSpace();

        foreach(var line in contentLines) {

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(line, GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        GUILayout.FlexibleSpace();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if(Drawer.Button($"<size=18>{Main.Lang.Get("YES", "Yes")}</size>", GUILayout.Width(150), GUILayout.Height(52))) {
            ConfirmDelete();
        }

        if(Drawer.Button($"<size=18>{Main.Lang.Get("NO", "No")}</size>", GUILayout.Width(150), GUILayout.Height(52))) {
            Close();
        }

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.EndVertical();
    }

    private void ConfirmDelete() {
        if(isClosing) {
            return;
        }

        try {
            obj?.Parent?.ObjectManager?.Destroy(obj);

            if(profile is not null) {
                ProfileManager.Destroy(profile);
            }

            OnDelete?.Invoke();
        } catch(Exception e) {
            Main.Logger?.Log("[DeletePopup] Delete failed: " + e);
        } finally {
            Close();
        }
    }

    private void Close() {
        if(isClosing) {
            return;
        }

        isClosing = true;
        Destroy(gameObject);
    }

    private static string SafeObjectName(OverlayerObject target) {
        try {
            return target?.Config?.Name ?? "<unknown>";
        } catch {
            return "<unknown>";
        }
    }

    private static string SafeProfileName(OverlayerProfile target) {
        try {
            return target?.Config?.Name ?? "<unknown>";
        } catch {
            return "<unknown>";
        }
    }
}