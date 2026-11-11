using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Overlayer.Core;
using Overlayer.Models;
using Overlayer.Unity;
using Overlayer.Utils;
using SFB;
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Overlayer.Views;

public class ImageConfigDrawer : ModelDrawable<ImageConfig> {
    public OverlayerImage image;

    public ImageConfigDrawer(OverlayerImage image) : base((ImageConfig)image.Config) => this.image = image;

    bool IsAdvensedMode => Main.Settings.UiMode == Settings.EditorUIMode.Advanced;

    public override void OnceCall() => NeoDrawer.StaticInstance.FieldResetDictById();

    public override void Draw() {
        NeoDrawer.StaticInstance.FieldResetId();
        Color old = GUI.color;

        GUILayout.BeginHorizontal();
        var oldMode = Main.Settings.UiMode;
        GUI.color = Main.Settings.UiMode == Settings.EditorUIMode.Simple ? Color.cyan : old;
        if(Drawer.Button(Main.Lang.Get("UI_SIMPLE", "Simple"), GUILayout.Width(120f), GUILayout.Height(32f))) {
            Main.Settings.UiMode = Settings.EditorUIMode.Simple;
        }
        GUI.color = Main.Settings.UiMode == Settings.EditorUIMode.Advanced ? Color.cyan : old;
        if(Drawer.Button(Main.Lang.Get("UI_ADVANCED", "Advanced"), GUILayout.Width(120f), GUILayout.Height(32f))) {
            Main.Settings.UiMode = Settings.EditorUIMode.Advanced;
        }
        GUI.color = old;
        GUILayout.EndHorizontal();
        if(oldMode != Main.Settings.UiMode) {
            NeoDrawer.StaticInstance.FieldClear();
        }

        if(Drawer.DrawBool(Drawer.Icon_Power, Main.Lang.Get("ACTIVE", "Active"), ref model.Active)) {
            image.gameObject.SetActive(model.Active);
        }
        bool _drag = model.Drag;
        Drawer.DrawBool(Drawer.Icon_Drag, Main.Lang.Get("DRAG", "Drag"), ref _drag);
        if(model.Drag != _drag) {
            model.Drag = _drag;
        }
        Drawer.DrawString(Drawer.Icon_Pencil, Main.Lang.Get("NAME", "Name"), ref model.Name);
        bool changed = false;
        changed |= Drawer.DrawExpr(Main.Lang.Get("POSITION", "Position"), "I_" + nameof(model.Position), ref model.Position, () => changed |= NeoDrawer.StaticInstance.DrawSize2(ref model.Position.Value, 0, 1), typeof(Vector2));
        changed |= Drawer.DrawExpr(Main.Lang.Get("SCALE", "Scale"), "I_" + nameof(model.Scale), ref model.Scale, () => changed |= NeoDrawer.StaticInstance.DrawSize2(ref model.Scale.Value, 0, 10), typeof(Vector2));
        if(IsAdvensedMode) {
            changed |= Drawer.DrawExpr(Main.Lang.Get("PIVOT", "Pivot"), "I_" + nameof(model.Pivot), ref model.Pivot, () => changed |= NeoDrawer.StaticInstance.DrawSize2(ref model.Pivot.Value, 0, 1), typeof(Vector2));
        }
        changed |= Drawer.DrawExpr(Main.Lang.Get("ROTATION", "Rotation"), "I_" + nameof(model.Rotation), ref model.Rotation, () => changed |= NeoDrawer.StaticInstance.DrawRotate3(ref model.Rotation.Value, -180, 180), typeof(Vector3));
        changed |= Drawer.DrawExpr(Drawer.Icon_Color, Main.Lang.Get("COLOR", "Color"), "I_" + nameof(model.Color), ref model.Color, () => {
            GUILayout.BeginHorizontal();
            changed |= NeoDrawer.StaticInstance.DrawColor(ref model.Color.Value);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }, typeof(Color));
        GUILayout.BeginHorizontal();
        if(Drawer.Button(Drawer.Icon_Image, GUILayout.Width(40))) {
            int session = Main.SessionGeneration;
            string title = Main.Lang.Get("SELECT_IMAGES", "Select Images");
            string modPath = Main.Mod.Path.TrimEnd('\\', '/');
            Task.Run(() => {
                try {
                    var extensions = new[] {
                        new ExtensionFilter("Image Files", "png", "jpg", "jpeg"),
                        new ExtensionFilter("All Files", "*")
                    };
                    string[] paths = StandaloneFileBrowser.OpenFilePanel(
                        title, Path.Combine(modPath, "Overlayer"), extensions, true
                    );
                    if(paths != null && paths.Length > 0) {
                        Main.MainThreadDispatcher.Enqueue(() => {
                            if(session != Main.SessionGeneration || !ProfileManager.Initialized || !this.image) {
                                return;
                            }
                            foreach(var path in paths) {
                                string p = (path.StartsWith(modPath, StringComparison.OrdinalIgnoreCase) && path.Length > modPath.Length && (path[modPath.Length] == '\\' || path[modPath.Length] == '/'))
                                    ? "{ModDir}" + path.Substring(modPath.Length).Replace("\\", "/")
                                    : path;
                                model.Images.Add(p);
                            }
                            this.image.ApplyImages();
                            ImageManager.CleanUp();
                        });
                    }
                } catch(Exception e) {
                    Main.Logger?.Log($"Failed to select images: {e}");
                }
            });
        }
        if(Drawer.Button(Drawer.Icon_Plus, GUILayout.Width(50))) {
            model.Images.Add(string.Empty);
            this.image.ApplyImages();
            changed = true;
        }
        if(Drawer.Button(Main.Lang.Get("RELOAD_IMAGES", "Reload"), GUILayout.Width(70))) {
            this.image.ApplyImages(true);
            ImageManager.CleanUp();
        }
        GUILayout.EndHorizontal();
        Drawer.BeginTab();
        for(int i = 0; i < model.Images.Count; i++) {
            GUILayout.BeginHorizontal();

            GUI.color = new Color(1f, 0.8f, 0.8f);
            if(Drawer.Button(Drawer.Icon_X, GUILayout.Width(46))) {
                ImageManager.Invalidate(model.Images[i]);
                model.Images.RemoveAt(i);
                this.image.ApplyImages();
                i--;
                ImageManager.CleanUp();
                GUI.color = old;
                GUILayout.EndHorizontal();
                continue;
            }

            GUI.color = old;

            if(Drawer.Button(Drawer.Icon_OpenFolder, GUILayout.Width(40))) {
                int session = Main.SessionGeneration;
                int index = i;
                var currentModel = model;
                string title = Main.Lang.Get("SELECT_IMAGE_FILE", "Select Image File");
                string modPath = Main.Mod.Path.TrimEnd('\\', '/');

                Task.Run(() => {
                    try {
                        var extensions = new[] {
                            new ExtensionFilter("Image Files", "png", "jpg", "jpeg"),
                            new ExtensionFilter("All Files", "*")
                        };
                        string[] paths = StandaloneFileBrowser.OpenFilePanel(
                            title, Path.Combine(modPath, "Overlayer"), extensions, false
                        );
                        if(paths != null && paths.Length > 0) {
                            string path = paths[0];
                            if((path.StartsWith(modPath, StringComparison.OrdinalIgnoreCase) && path.Length > modPath.Length && (path[modPath.Length] == '\\' || path[modPath.Length] == '/'))) {
                                path = "{ModDir}" + path.Substring(modPath.Length).Replace("\\", "/");
                            }
                            string finalPath = path;
                            Main.MainThreadDispatcher.Enqueue(() => {
                                if(session != Main.SessionGeneration || !ProfileManager.Initialized ||
                                   !this.image || index >= currentModel.Images.Count) {
                                    return;
                                }
                                ImageManager.Invalidate(currentModel.Images[index]);
                                currentModel.Images[index] = finalPath;
                                this.image.ApplyImages();
                                ImageManager.CleanUp();
                            });
                        }
                    } catch(Exception e) {
                        Main.Logger?.Log($"Failed to select image: {e}");
                    }
                });
            }

            string img = model.Images[i];
            if(Drawer.DrawOnlyString(ref img)) {
                ImageManager.Invalidate(model.Images[i]);
                model.Images[i] = img;
                this.image.ApplyImages();
                ImageManager.CleanUp();
            }

            GUILayout.EndHorizontal();
            if(i < image.ImageStates.Count && image.ImageStates[i] != ImageManager.ImageLoadState.Loaded) {
                GUILayout.Label(image.ImageStates[i] switch {
                    ImageManager.ImageLoadState.Empty => Main.Lang.Get("IMAGE_NO_FILE", "No file selected"),
                    ImageManager.ImageLoadState.Missing => Main.Lang.Get("IMAGE_MISSING", "Image file missing"),
                    _ => Main.Lang.Get("IMAGE_INVALID", "Cannot read image")
                });
            }
        }
        Drawer.EndTab();
        changed |= Drawer.DrawCodeEditor(Drawer.Icon_Play, Main.Lang.Get("PLAYING_COMMAND", "Playing Command"), model.Name + "PlayingCommand", ref model.PlayingCommand);
        changed |= Drawer.DrawCodeEditor(Drawer.Icon_Pause, Main.Lang.Get("NOT_PLAYING_COMMAND", "Not Playing Command"), model.Name + "NotPlayingCommand", ref model.NotPlayingCommand);
        GUILayout.BeginHorizontal();
        GUI.color = new Color(1f, 0.8f, 1f);
        if(Drawer.Button(Drawer.Icon_Up, GUILayout.Width(46))) {
            try {
                JObject node = model.Serialize() as JObject;
                node["Type"] = "Image";
                var referencePaths = Main.Settings.IncludeReferences ? ImageConfigImporter.GetJustReferences(model) : null;
                string title = Main.Lang.Get("EXPORT_IMAGE_CONFIG", "Export Image Config");
                string saveFolder = Persistence.GetLastUsedFolder();
                string fileName = $"{model.Name}.json";
                Task.Run(() => {
                    try {
                        string target = StandaloneFileBrowser.SaveFilePanel(title, saveFolder, fileName, "json");
                        if(!string.IsNullOrWhiteSpace(target)) {
                            if(referencePaths != null) {
                                node["References"] = ProfileReferences.ResolveReferences(referencePaths);
                            }
                            Overlayer.Utils.Extensions.WriteAllTextAtomic(target, JsonConvert.SerializeObject(node, Formatting.Indented));
                        }
                    } catch(Exception e) {
                        Main.Logger?.Log($"Failed to export image config '{fileName}': {e}");
                    }
                });
            } catch(Exception e) {
                Main.Logger?.Log($"Failed to prepare image config export: {e}");
            }
        }
        GUI.color = new Color(1f, 0.8f, 0.8f);
        if(Drawer.Button(Drawer.Icon_X, GUILayout.Width(46))) {
            image.Parent.ObjectManager.Destroy(image);
            GUI.color = old;
            GUILayout.EndHorizontal();
            Main.GUI.Pop();
            return;
        }
        GUI.color = old;
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        if(changed) {
            image.ApplyConfig();
        }

        NeoDrawer.StaticInstance.UpdateFocused();
    }
}