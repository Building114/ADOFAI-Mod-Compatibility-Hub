﻿using DG.Tweening;
using Overlayer.Core;
using RapidGUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Overlayer.Utils;

internal class UpdatePopup : MonoBehaviour {
    private const int MaxUpdateLines = 400;
    private const int MaxUpdateCharacters = 32000;
    private const int MaxSingleLineCharacters = 1200;
    private const int MaxDisplayLineCharacters = 160;
    private const float MinWindowWidth = 420f;
    private const float MinWindowHeight = 260f;
    private const float MaxWindowWidthRatio = 0.82f;
    private const float MaxWindowHeightRatio = 0.82f;
    private const float ContentPadding = 40f;
    private const float ButtonAreaHeight = 125f;
    private const float NonScrollExtraHeight = 170f;
    private const float ApproxLineHeight = 20f;

    private Rect windowRect;
    private Vector2 scrollPosition;
    private string version = "";
    private string[] contentLines;
    private string filePath;
    private string fileFingerprint;
    private string deletionError;
    private bool readFailed;
    private bool isAskingToDelete;
    private bool isInitialized = false;
    private bool isAnimating = false;
    private bool hasCalculatedWindowRect = false;
    private int lastScreenWidth = 0;
    private int lastScreenHeight = 0;

    public void Initialize() {
        version = Main.Mod.Version.ToString();

        filePath = Path.Combine(Main.Mod.Path, "update.txt");
        if(!File.Exists(filePath)) {
            Destroy(gameObject);
            return;
        }

        try {
            contentLines = ReadUpdateLines(filePath);
            fileFingerprint = Main.GetUpdateLogFingerprint(filePath);
        } catch(Exception ex) {
            readFailed = true;
            contentLines = new[] {
                "Failed to read update.txt.",
                ex.Message
            };
        }

        if(contentLines == null || contentLines.Length == 0) {
            contentLines = new[] { "update.txt is empty." };
        }

                                                                                         
                                                                                               
        isInitialized = true;
    }

    private static string[] ReadUpdateLines(string filePath) {
        var lines = new List<string>();
        var readCharacters = 0;
        var truncated = false;

        using(var reader = new StreamReader(filePath, true)) {
            string line;
            while((line = reader.ReadLine()) != null) {
                readCharacters += line.Length + 1;

                if(line.Length > MaxSingleLineCharacters) {
                    line = line.Substring(0, MaxSingleLineCharacters) + " ...";
                    truncated = true;
                }

                AddDisplayLine(lines, line);

                if(lines.Count > MaxUpdateLines) {
                    lines.RemoveRange(MaxUpdateLines, lines.Count - MaxUpdateLines);
                    truncated = true;
                    break;
                }

                if(lines.Count >= MaxUpdateLines || readCharacters >= MaxUpdateCharacters) {
                    truncated = truncated || !reader.EndOfStream;
                    break;
                }
            }
        }

        if(truncated) {
            var shownLines = lines.Count;
            lines.Add("");
            lines.Add($"... update.txt is too long, so only the first {shownLines} lines / {MaxUpdateCharacters} characters are shown.");
        }

        return lines.ToArray();
    }

    private static void AddDisplayLine(List<string> lines, string line) {
        if(string.IsNullOrEmpty(line)) {
            lines.Add("");
            return;
        }

        for(var index = 0; index < line.Length; index += MaxDisplayLineCharacters) {
            var length = Math.Min(MaxDisplayLineCharacters, line.Length - index);
            lines.Add(line.Substring(index, length));
        }
    }

    private void CalculateWindowRect() {
        var maxAllowedWidth = Mathf.Max(240f, Screen.width * MaxWindowWidthRatio);
        var maxAllowedHeight = Mathf.Max(180f, Screen.height * MaxWindowHeightRatio);
        var minAllowedWidth = Mathf.Min(MinWindowWidth, maxAllowedWidth);
        var minAllowedHeight = Mathf.Min(MinWindowHeight, maxAllowedHeight);
        var maxContentWidth = 0f;

        var labelStyle = GUI.skin != null ? new GUIStyle(GUI.skin.label) { richText = false } : null;

        foreach(var line in contentLines) {
            var lineWidth = labelStyle != null
                ? labelStyle.CalcSize(new GUIContent(line)).x
                : line.Length * 8f;

            if(lineWidth > maxContentWidth) {
                maxContentWidth = lineWidth;
            }
        }

        var width = Mathf.Clamp(Mathf.Min(maxContentWidth + ContentPadding, maxAllowedWidth), minAllowedWidth, maxAllowedWidth);
        var wantedHeight = contentLines.Length * ApproxLineHeight + ButtonAreaHeight;
        var height = Mathf.Clamp(wantedHeight, minAllowedHeight, maxAllowedHeight);

        windowRect = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        hasCalculatedWindowRect = true;
    }

    private void OnGUI() {
        if(!isInitialized || contentLines == null || contentLines.Length == 0) {
            return;
        }

        if(!hasCalculatedWindowRect || lastScreenWidth != Screen.width || lastScreenHeight != Screen.height) {
            CalculateWindowRect();
        }

        var title = $"Overlayer {version} {Main.Lang.Get("UPDATE", "Update")}";

                                                
                                                                                                   
                                                                                                      
        windowRect = GUI.Window(120, windowRect, DrawWindow, title, RGUIStyle.darkWindow);
    }

    private void DrawWindow(int windowID) {
        GUI.BringWindowToFront(windowID);

        GUILayout.BeginVertical();
        GUILayout.Space(10);

        var labelStyle = new GUIStyle(GUI.skin.label) {
            wordWrap = true,
            richText = false
        };

        var scrollHeight = Mathf.Max(80f, windowRect.height - NonScrollExtraHeight);

        using(var scrollScope = new GUILayout.ScrollViewScope(scrollPosition, GUILayout.Height(scrollHeight))) {
            scrollPosition = scrollScope.scrollPosition;
            GUILayout.BeginVertical();

            foreach(var line in contentLines) {
                GUILayout.Label(line, labelStyle, GUILayout.ExpandWidth(true));
            }

            GUILayout.EndVertical();
        }

        GUILayout.Space(10);

        if(isAskingToDelete) {
            var promptStyle = new GUIStyle(GUI.skin.label) {
                wordWrap = true,
                richText = false,
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label(Main.Lang.Get("UPDATE_LOG_DELETE_PROMPT",
                "Delete update.txt? If you keep it, you can reopen it from the Overlayer menu."), promptStyle);
            if(!string.IsNullOrEmpty(deletionError)) {
                GUILayout.Label(deletionError, promptStyle);
            }
        }

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if(isAskingToDelete) {
            if(Drawer.Button(Main.Lang.Get("UPDATE_LOG_DELETE", "Delete file"), GUILayout.Width(140), GUILayout.Height(40))) {
                FinishReading(true);
            }
            if(Drawer.Button(Main.Lang.Get("UPDATE_LOG_KEEP", "Keep file"), GUILayout.Width(140), GUILayout.Height(40))) {
                FinishReading(false);
            }
        } else if(Drawer.Button(Main.Lang.Get("UPDATE_LOG_DONE", "Finished reading"), GUILayout.Width(170), GUILayout.Height(40))) {
            if(readFailed) {
                AnimateAndDestroy();
            } else {
                isAskingToDelete = true;
            }
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.EndVertical();

                                                                   
                                                                
    }

    private void FinishReading(bool deleteFile) {
        if(isAnimating) {
            return;
        }

        if(deleteFile && File.Exists(filePath)) {
            try {
                if(Main.GetUpdateLogFingerprint(filePath) != fileFingerprint) {
                    deletionError = Main.Lang.Get("UPDATE_LOG_CHANGED",
                        "update.txt has changed since it was opened. Close and reopen the log before deleting it.");
                    return;
                }

                File.Delete(filePath);
            } catch(Exception ex) {
                Main.Logger?.Log("Failed to delete update.txt: " + ex);
                deletionError = Main.Lang.Get("UPDATE_LOG_DELETE_FAILED",
                    "Could not delete update.txt. The file is still kept.") + " " + ex.Message;
                return;
            }
        }

        Main.MarkUpdateLogRead(fileFingerprint);
        AnimateAndDestroy();
    }

    private IEnumerator DestroyCoroutine() {
        yield return new WaitForSecondsRealtime(0.5f);
        Destroy(gameObject);
    }

    private void AnimateAndDestroy() {
        if(isAnimating) {
            return;
        }

        isAnimating = true;

        StartCoroutine(DestroyCoroutine());
        DOTween.To(() => windowRect.position, x => windowRect.position = x,
                new Vector2(windowRect.position.x, Screen.height * -1.3f), 0.4f)
            .SetEase(Ease.InBack)
            .SetUpdate(true);
    }
}
