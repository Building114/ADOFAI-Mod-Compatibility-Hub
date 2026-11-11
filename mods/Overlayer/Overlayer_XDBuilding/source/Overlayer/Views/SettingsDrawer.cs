using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Overlayer.Core;
using Overlayer.Core.Patches;
using Overlayer.Core.Scripting;
using Overlayer.Core.Translation;
using Overlayer.Models;
using Overlayer.Patches;
using Overlayer.Tags;
using Overlayer.Unity;
using Overlayer.Utils;
using RapidGUI;
using SFB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Time = UnityEngine.Time;

namespace Overlayer.Views;

public class SettingsDrawer : ModelDrawable<Settings> {
    public SettingsDrawer(Settings settings) : base(settings) { }

    private enum ExtraMenus {
        Closed,
        Overlayer,
        Scripting,
        Adofai,
    }

    private ExtraMenus extraMenu = ExtraMenus.Closed;
    private string[] languages;
    private string[] userLanguages;
    internal bool NeedLangInit = true;

    public readonly static string[] preparingsymbols = { "|", "/", "-", "\\" };
    public float preparinglastUpdateTime = 0f;
    public int preparingsymbolIndex = 0;
    private float languageLoadingStartedAt = -1f;
    private bool drawLoadingState;
    private bool drawLoadingStateInitialized;

    private void LanguageInit() {
        languageLoadingStartedAt = -1f;
        preparingsymbolIndex = 0;
        languages = Main.Lang.GetLanguages();
        userLanguages = Main.Lang.GetLanguageNativeNames();
    }

    private void LanguageUpdate(int index) {
        Main.Lang.Language = languages[index];
        model.Lang = Main.Lang.Language;
    }

    public override void OnceCall() {
        NeoDrawer.StaticInstance.FieldResetDictById();
        LanguageInit();
    }

    private bool needCreateNewProfile = false;

    private int[] dragSoltRange;
    private bool dragSoltNeedInit = true;
    private int dragSoltDragging = -1;
    private int dragSoltInsert = -1;

    public override void Draw() {
        NeoDrawer.StaticInstance.FieldResetId();

        if(Main.Logo != null && !model.DisableLogo) {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Main.Logo, GUILayout.Width(Main.Logo.width), GUILayout.Height(Main.Logo.height));
            GUILayout.BeginVertical();
            Rect v3labelRect = GUILayoutUtility.GetRect(new GUIContent("Overlayer v3"), GUI.skin.label, GUILayout.Height(62));
            GUI.Label(v3labelRect, "<size=62>Overlayer v3</size>");
            if(Event.current.type == EventType.MouseDown && v3labelRect.Contains(Event.current.mousePosition) && !Main.EgEnabled) {
                egHandle();
                Event.current.Use();
            }
            GUILayout.Label($"<size=26>{Main.Lang.Get("SLOGAN_TEXT", "Display everything as you wish.")}</size>");
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<size=16>{Main.Mod.Version}, by </size>");
            GUILayout.Label($"<size=16><color=#{Effect.Rainbow(11.0)}>Square3ang</color></size>");
            GUILayout.Label($"<size=16> & </size>");
            GUILayout.Label($"<size=16><color=#{Effect.Rainbow(9.0)}>Kkitut</color></size>");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if(Main.EgEnabled) {
                Main.Eg.DrawChoices();
            }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        if(Event.current.type == EventType.Layout || !drawLoadingStateInitialized) {
            drawLoadingState = Main.Lang.IsLoading;
            drawLoadingStateInitialized = true;
        }
        if(drawLoadingState) {
            if(languageLoadingStartedAt < 0f) languageLoadingStartedAt = Time.realtimeSinceStartup;
            float elapsedTime = Time.time - preparinglastUpdateTime;

            if(elapsedTime >= 0.05f) {
                preparingsymbolIndex++;
                if(preparingsymbolIndex >= preparingsymbols.Length) {
                    preparingsymbolIndex = 0;
                }
                preparinglastUpdateTime = Time.time;
            }

            bool delayed = Time.realtimeSinceStartup - languageLoadingStartedAt >= 8f;
            string message = delayed
                ? "Is the Preparing is taking too long?? please get in touch with the developer for assistance!!... No. Don;t do that again,,,, now go to check the Overlayer log."
                : "Loading translations for you, hang tight...";
            GUILayout.Label(message + " " + preparingsymbols[preparingsymbolIndex]);
        } else {
            languageLoadingStartedAt = -1f;
            string languageDesc;
            if(Main.Lang.IsDefault) {
                languageDesc = $"! {Translator.FALLBACK_LANGUAGE} by OVERLAYER";
            } else {
                int translatorsCount = Main.Lang.GetArrCount("0TRANSLATORS");

                if(translatorsCount > 0) {
                    var names = new List<string>();
                    for(int i = 0; i < translatorsCount; i++) {
                        names.Add(Main.Lang.GetArr("0TRANSLATORS", i, "[UNKNOWN]"));
                    }
                    string translatorsText = string.Join(" & ", names);
                    languageDesc = $"| {Main.Lang.Get("0NATIVELANG", Main.Lang.Language)} by {translatorsText}";
                } else {
                    languageDesc = $"| {Main.Lang.Language}";
                }
            }

            GUILayout.Label($"{Main.Lang.Get("SELECTLANGUAGE", "Select Language")} {languageDesc}");
            if(Main.Lang.IsSomeFail) {
                GUILayout.Label("<color=#FFFF00>Some translations could not be loaded. Check the Overlayer log for details.</color>");
            } else if(Main.Lang.IsFail) {
                GUILayout.Label($"<color=#FF0000>All translations failed to load: {Main.Lang.FailState}</color>");
            }
            GUILayout.BeginHorizontal();
            if(languages == null || languages.Length == 0 || userLanguages == null || userLanguages.Length == 0) {
                languages = [Translator.FALLBACK_LANGUAGE];
                userLanguages = ["Default"];
            }
            int selectedIndex = Array.IndexOf(languages, Main.Lang.Language);
            if(selectedIndex < 0) selectedIndex = 0;

            if(Drawer.Button("◀", GUILayout.Width(40))) {
                selectedIndex = (selectedIndex - 1 + languages.Length) % languages.Length;
                LanguageUpdate(selectedIndex);
            }

            if(Drawer.SelectionPopup(ref selectedIndex, userLanguages, "", GUILayout.Width(400))) {
                LanguageUpdate(selectedIndex);
            }
            if(Drawer.Button("▶", GUILayout.Width(40))) {
                selectedIndex = (selectedIndex + 1) % languages.Length;
                LanguageUpdate(selectedIndex);
            }

            bool reloadLang = false;
            // I LOVE UNITY SO MUCH WTF
            try {
                // F###! WHY 'System.ArgumentException'?????? WHY??????????????
                reloadLang = Drawer.Button(Main.Lang.Get("RELOADLANG", "Reload Language Pack"), GUILayout.Width(320));
            } catch {
            } finally {
                GUILayout.EndHorizontal();
            }

            if(reloadLang) {
                _ = Main.Lang.Load(Path.Combine(Main.Mod.Path, "lang"));
            }
        }
        GUILayout.BeginHorizontal();
        DrawMenuButton("Overlayer", ExtraMenus.Overlayer);
        DrawMenuButton("Scripting", ExtraMenus.Scripting);
        DrawMenuButton("ADOFAI", ExtraMenus.Adofai);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        switch(extraMenu) {
            case ExtraMenus.Overlayer:
                if(Drawer.DrawBool(string.Format(Main.Lang.Get("DISABLE_THIS", "Disable {0}"), Main.Lang.Get("LOGO", "Logo")), ref model.DisableLogo)) {
                    if(model.DisableLogo) {
                        Main.LogoRelease();
                    } else {
                        Main.LogoInit(Main.Mod.Path);
                    }
                }
                model.AutoUpdate = false;
                model.AutoUpdateBeta = false;
                Drawer.DrawBool(string.Format(Main.Lang.Get("USE_THIS", "Use {0}"), Main.Lang.Get("TOOLTIP", "Tooltip")), ref model.Tooltip);
                Drawer.HoverTooltip(Main.Lang.Get("TOOLTIP_DESC", "You are currently viewing this"));
                if(Drawer.DrawBool(Main.Lang.Get("LEGACY_THEME", "Legacy Theme"), ref model.LegacyTheme)) {
                    Drawer.SetStyle(model.LegacyTheme);
                    RGUIStyle.CreateStyles();
                }
                Drawer.HoverTooltip(Main.Lang.Get("LEGACY_THEME_DESC", "Reverts Overlayer’s custom clean theme back to Unity’s default OnGUI theme"));
                Drawer.DrawBool(string.Format(Main.Lang.Get("AUTO_THIS", "Auto {0}"), Main.Lang.Get("PIVOT", "Pivot")), ref model.AutoPivot);
                Drawer.HoverTooltip(Main.Lang.Get("AUTO_PIVOT_DESC", "Automatically adjusts the pivot to match the text alignment.\nWhen the editor is in Simple mode, this is always enabled regardless of settings"));
                Drawer.DrawBool(Main.Lang.Get("INCLUDE_REFERENCES", "Include References"), ref model.IncludeReferences);
                Drawer.HoverTooltip(Main.Lang.Get("INCLUDE_REFERENCES_DESC", "When this setting is enabled,\nelements that rely on external files(such as fonts and images)\nwill be included directly in the JSON file export.\n\nThis may increase the file size"));
                Drawer.DrawBool(string.Format(Main.Lang.Get("USE_THIS", "Use {0}"), string.Format(Main.Lang.Get("SAFE_COMMAND_PARSE", "Safe Command Parse"))), ref model.SafeCommandParse);
                Drawer.HoverTooltip(Main.Lang.Get("SAFE_COMMAND_PARSE_DESC", "Parses commands safely by catching errors.\nReturns a default value instead of throwing exceptions on failure.\n\nMay hide underlying errors"));
                if(Drawer.DrawBool(string.Format(Main.Lang.Get("USE_THIS", "Use {0}"), string.Format(Main.Lang.Get("FILE_ATTEMPT", "File Attempt"))), ref model.FileAttempt)) {
                    if(model.FileAttempt) {
                        Main.FileAttempt ??= new FileAttempt();
                        SafePatchController.ApplyPatch<FileAttemptLoadPatch>();
                        SafePatchController.ApplyPatch<FileAttemptSavePatch>();
                        if(scnGame.instance is not null) {
                            Main.FileAttempt.Load();
                        }
                    } else {
                        if(scnGame.instance is not null) {
                            Main.FileAttempt.Save();
                        }
                        SafePatchController.RemovePatch<FileAttemptSavePatch>();
                        SafePatchController.RemovePatch<FileAttemptLoadPatch>();
                        Main.FileAttempt = null;
                    }
                }
                Drawer.HoverTooltip(Main.Lang.Get("FILE_ATTEMPT_DESC", "You can use FileAttempts & FileTileAttempts Tags when enabled"));
                Drawer.DrawBool(string.Format(Main.Lang.Get("THIS_EDITOR", "{0} Editor"), nameof(Effect.MovingMan)), ref model.MovingManEditor);
                Drawer.DrawBool(string.Format(Main.Lang.Get("THIS_EDITOR", "{0} Editor"), nameof(Effect.ColorRange)), ref model.ColorRangeEditor);
                Drawer.DrawBool(string.Format(Main.Lang.Get("THIS_EDITOR", "{0} Editor"), nameof(Effect.EasedValue)), ref model.EasedValueEditor);
                NeoDrawer.StaticInstance.DrawSingle(Main.Lang.Get("FPS_UPDATE_RATE", "Fps Update Rate"), ref model.FPSUpdateRate);
                NeoDrawer.StaticInstance.DrawSingle(Main.Lang.Get("FRAMETIME_UPDATE_RATE", "FrameTime Update Rate"), ref model.FrameTimeUpdateRate);
                NeoDrawer.StaticInstance.DrawInt32(Main.Lang.Get("SYSTEMTAG_UPDATE_RATE", "System Tag Update Rate"), ref model.SystemTagUpdateRate);
                Drawer.HoverTooltip(Main.Lang.Get("SYSTEMTAG_UPDATE_RATE_DESC", "Such as GC usage Tags"));
                break;
            case ExtraMenus.Scripting:
                Scripting.DrawUI();
                break;
            case ExtraMenus.Adofai:
                if(Drawer.DrawBool(Main.Lang.Get("CHANGE_FONT", "Change Font"), ref model.ChangeFont)) {
                    if(!model.ChangeFont) {
                        model.AdofaiFont.name = "Default";
                        if(model.AdofaiFont.Apply(out var font)) {
                            FontManager.SetFont(model.AdofaiFont.name, font);
                            RDString.initialized = false;
                            RDString.Setup();
                            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                        }
                    }
                }
                if(model.ChangeFont) {
                    Drawer.BeginTab();
                    Drawer.DrawString(Main.Lang.Get("FONT", "Font"), ref model.AdofaiFont.name);
                    NeoDrawer.StaticInstance.DrawSingle(Main.Lang.Get("FONT_SIZE", "Font Scale"), ref model.AdofaiFont.fontScale);
                    NeoDrawer.StaticInstance.DrawSingle(Main.Lang.Get("LINE_SPACING", "Font Line Spacing"), ref model.AdofaiFont.lineSpacing);
                    GUILayout.BeginHorizontal();
                    if(Drawer.Button(Main.Lang.Get("APPLY", "Apply"))) {
                        if(model.AdofaiFont.Apply(out var font)) {
                            FontManager.SetFont(model.AdofaiFont.name, font);
                            RDString.initialized = false;
                            RDString.Setup();
                            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                        }
                    }
                    if(Drawer.Button(Main.Lang.Get("LOG_FONT_LIST", "Log Font List"))) {
                        foreach(var font in FontManager.OSFonts) {
                            Main.Logger.Log(font);
                        }
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    Drawer.EndTab();
                }
                if(Drawer.DrawBool(string.Format(Main.Lang.Get("USE_THIS", "Use {0}"), string.Format(Main.Lang.Get("SHOW_TRUE_AUTO_JUDGMENT", "Show True Auto Judgment"))), ref model.ShowTrueAutoJudgment)) {
                    if(model.ShowTrueAutoJudgment) {
                        SafePatchController.ApplyPatch<HitFixPatch>();
                    } else {
                        SafePatchController.RemovePatch<HitFixPatch>();
                    }
                }
                Drawer.HoverTooltip(Main.Lang.Get("SHOW_TRUE_AUTO_JUDGEMENT_DESC", "Tries to show judgements on the Hit Error Meter during Autoplay (may not work in 3.4.0)"));
                break;
        }
        if(extraMenu != ExtraMenus.Closed) {
            GUILayout.Space(12);
        }

        Color old = GUI.color;
        GUILayout.BeginHorizontal();
        if(Drawer.Button(Drawer.Icon_Plus, GUILayout.Width(100))) {
            needCreateNewProfile = true;
        }
        GUI.color = new Color(1f, 1f, 0.8f);
        if(Drawer.Button(Drawer.Icon_Down, GUILayout.Width(60))) {
            int session = Main.SessionGeneration;
            string title = Main.Lang.Get("SELECT_PROFILE", "Select Profile");
            string filterName = Main.Lang.Get("OVERLAYER_PROFILE_JSON", "Overlayer Profile JSON");
            string profilePath = Main.ProfilePath;
            var existingProfiles = new HashSet<string>(
                ProfileManager.Profiles.Where(p => p?.Config != null).Select(p => p.Config.Name),
                StringComparer.OrdinalIgnoreCase
            );
            Task.Run(() => {
                try {
                    string[] pfs = StandaloneFileBrowser.OpenFilePanel(
                        title, profilePath, [new ExtensionFilter(filterName, "json")], true
                    );
                    if(pfs == null) {
                        return;
                    }
                    foreach(var pf in pfs) {
                        try {
                            if(!string.Equals(Path.GetExtension(pf), ".json", StringComparison.OrdinalIgnoreCase)) {
                                continue;
                            }
                            string name = Path.GetFileNameWithoutExtension(pf);
                            if(existingProfiles.Contains(name)) {
                                continue;
                            }
                            string targetPath = Path.Combine(profilePath, name + ".json");
                            bool needCopy = !string.Equals(
                                Path.GetFullPath(pf),
                                Path.GetFullPath(targetPath),
                                StringComparison.OrdinalIgnoreCase
                            );
                            if(needCopy && File.Exists(targetPath)) {
                                Main.Logger?.Log($"Skipped profile import '{pf}': destination already exists.");
                                continue;
                            }
                            var content = File.ReadAllText(pf);
                            if(string.IsNullOrWhiteSpace(content)) {
                                continue;
                            }
                            var token = JToken.Parse(content);
                            var cfg = new ProfileConfig();
                            cfg.Deserialize(token);
                            if(needCopy) {
                                File.Copy(pf, targetPath, false);
                            }
                            cfg.Path = targetPath;
                            cfg.Name = name;

                            if(cfg.MigratedFromLegacyFormat) {
                                string backup = targetPath + ".legacy.bak";
                                if(!File.Exists(backup)) {
                                    File.Copy(targetPath, backup, false);
                                }
                                Overlayer.Utils.Extensions.WriteAllTextAtomic(targetPath, cfg.Serialize().ToString(Newtonsoft.Json.Formatting.Indented));
                            }

                            Main.MainThreadDispatcher.Enqueue(() => {
                                if(session != Main.SessionGeneration || !ProfileManager.Initialized ||
                                   ProfileManager.Profiles == null || ProfileManager.Exists(name)) {
                                    return;
                                }
                                try {
                                    JToken refsNode = token is JObject ? token : null;
                                    foreach(var obj in cfg.Objects) {
                                        if(refsNode != null && obj is TextConfig t) {
                                            TextConfigImporter.ImportRef(t, refsNode);
                                        } else if(refsNode != null && obj is ImageConfig i) {
                                            ImageConfigImporter.ImportRef(i, refsNode);
                                        }
                                    }
                                    var profile = ProfileManager.CreateRuntimeProfile(cfg);
                                    if(profile != null) {
                                        ProfileManager.Profiles.Add(profile);
                                        TagManager.UpdatePatch();
                                    }
                                    dragSoltNeedInit = true;
                                } catch(Exception ex) {
                                    Main.Logger?.Log($"Failed to create profile '{pf}' on main thread: {ex}");
                                }
                            });
                        } catch(Exception e) {
                            Main.Logger?.Log($"Failed to load profile '{pf}': {e}");
                        }
                    }
                } catch(Exception e) {
                    Main.Logger?.Log($"Failed to select profiles for import: {e}");
                }
            });
        }
        GUI.color = old;
        if(Drawer.Button(Drawer.Icon_OpenFolder, GUILayout.Width(80))) {
            Application.OpenURL(Path.GetFullPath(Main.Mod.Path));
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        bool isRepaint = Event.current.type == EventType.Repaint;
        if(dragSoltNeedInit) {
            dragSoltNeedInit = false;
            dragSoltRange = new int[ProfileManager.Profiles.Count];
        }

        for(int i = 0; i < ProfileManager.Profiles.Count; i++) {
            var profile = ProfileManager.Get(i);
            if(profile == null) {
                GUILayout.Label($"[{Main.Lang.Get("ERROR", "Error")}] " + string.Format(Main.Lang.Get("ERROR_THIS_PROFILE_INDEX", "Unable to load profile data at index {0}"), i.ToString()));
                continue;
            }

            if(i != dragSoltDragging) {
                if(i == dragSoltInsert) {
                    GUILayout.BeginHorizontal();
                    bool dummy = false;
                    Color oldd = GUI.color;
                    GUI.color = Color.black;
                    Drawer.DrawOnlyBool(ref dummy);
                    GUI.color = oldd;
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
                GUILayout.BeginHorizontal();
                if(Drawer.DrawOnlyBool(ref profile.Config.Active)) {
                    profile.ApplyConfig();
                    TagManager.UpdatePatch();
                }
                GUILayout.Label("-==-", GUI.skin.label);
                if(dragSoltDragging < 0 && Event.current.type == EventType.MouseDown && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition)) {
                    dragSoltDragging = i;
                    dragSoltInsert = i;
                }
                GUILayout.Space(6);
                GUI.color = profile.Config.Active ? new Color(0.8f, 0.8f, 1f) : Color.gray;
                GUI.enabled = profile.Config.Active;
                if(Drawer.Button(Drawer.Icon_Pencil, GUILayout.Width(80))) {
                    Main.GUI.Push(new ProfileDrawer(profile));
                }
                GUI.enabled = true;
                GUI.color = new Color(1f, 0.8f, 1f);
                if(Drawer.Button(Drawer.Icon_Up, GUILayout.Width(46))) {
                    try {
                        profile.Config.Objects = profile.ObjectManager.Export();
                        var node = profile.Config.Serialize();
                        var referencePaths = model.IncludeReferences ? ProfileReferences.GetJustReferences(profile) : null;
                        string title = Main.Lang.Get("EXPORT_PROFILE", "Export Profile");
                        string saveFolder = Persistence.GetLastUsedFolder();
                        string fileName = $"{profile.Config.Name}.json";
                        Task.Run(() => {
                            try {
                                string target = StandaloneFileBrowser.SaveFilePanel(title, saveFolder, fileName, "json");
                                if(!string.IsNullOrWhiteSpace(target)) {
                                    if(referencePaths != null) {
                                        node["References"] = ProfileReferences.ResolveReferences(referencePaths);
                                    }
                                    Overlayer.Utils.Extensions.WriteAllTextAtomic(target, node.ToString());
                                }
                            } catch(Exception e) {
                                Main.Logger?.Log($"Failed to export profile '{fileName}': {e}");
                            }
                        });
                    } catch(Exception e) {
                        Main.Logger?.Log($"Failed to prepare profile export: {e}");
                    }
                }
                GUI.color = new Color(1f, 0.8f, 0.8f);
                if(Drawer.Button(Drawer.Icon_X, GUILayout.Width(46))) {
                    if(Event.current.shift) {
                        ProfileManager.Destroy(profile);
                    } else {
                        if(UnityEngine.Object.FindAnyObjectByType<DeletePopup>() == null) {
                            var popup = new GameObject().AddComponent<DeletePopup>();
                            UnityEngine.Object.DontDestroyOnLoad(popup);
                            popup.Initialize(profile, () => {
                                ProfileManager.Destroy(profile);
                                dragSoltNeedInit = true;
                            });
                        }
                    }
                    GUI.color = old;
                    GUILayout.EndHorizontal();
                    return;
                }
                GUI.color = old;
                GUILayout.Label(profile.Config.Name);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            if(Event.current.type == EventType.Repaint) {
                dragSoltRange[i] = Mathf.RoundToInt(GUILayoutUtility.GetLastRect().y);
            }
        }

        if(dragSoltInsert == ProfileManager.Count) {
            GUILayout.BeginHorizontal();
            bool dummy = false;
            Color oldd = GUI.color;
            GUI.color = Color.black;
            Drawer.DrawOnlyBool(ref dummy);
            GUI.color = oldd;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        if(dragSoltDragging >= 0) {
            if(Event.current.type == EventType.MouseUp) {
                ProfileManager.OrderByDrag(dragSoltDragging, dragSoltInsert);

                dragSoltDragging = -1;
                dragSoltInsert = -1;

                GUILayout.BeginArea(Rect.zero);
                GUILayout.BeginHorizontal();
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            } else {
                if(isRepaint) {
                    int insertIndex = -1;
                    for(int i = 0; i < dragSoltRange.Length; i++) {
                        if(Event.current.mousePosition.y - 14 <= dragSoltRange[i]) {
                            insertIndex = i;
                            break;
                        } else if(i == dragSoltRange.Length - 1) {
                            insertIndex = dragSoltRange.Length;
                            break;
                        }
                    }
                    dragSoltInsert = insertIndex;
                }

                float dragWidth = Screen.width;
                float dragHeight = 24;
                Rect dragRect = new(
                    GUILayoutUtility.GetLastRect().x,
                    Event.current.mousePosition.y - (dragHeight * 3),
                    dragWidth,
                    dragHeight
                );

                var dpf = ProfileManager.Get(dragSoltDragging);

                GUILayout.BeginArea(dragRect);
                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                bool dmyActive = dpf.Config.Active;
                Drawer.DrawOnlyBool(ref dmyActive);
                GUILayout.Label("-==-", GUI.skin.label);
                GUILayout.Space(6);
                GUI.color = new Color(0.8f, 0.8f, 1f);
                Drawer.ButtonDummy(Drawer.Icon_Pencil, GUILayout.Width(80));
                GUI.color = new Color(1f, 0.8f, 1f);
                Drawer.ButtonDummy(Drawer.Icon_Up, GUILayout.Width(46));
                GUI.color = new Color(1f, 0.8f, 0.8f);
                Drawer.ButtonDummy(Drawer.Icon_X, GUILayout.Width(46));
                GUI.color = old;
                GUILayout.Label(dpf.name);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }
        } else {
            GUILayout.BeginArea(Rect.zero);
            GUILayout.BeginHorizontal();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        if(NeedLangInit) {
            NeedLangInit = false;
            languages = null;
            userLanguages = null;
            LanguageInit();
        }

        if(needCreateNewProfile) {
            needCreateNewProfile = false;

            Directory.CreateDirectory(Main.ProfilePath);

            int i = 1;
            string name;
            do {
                name = $"Profile {i}";
                i++;
            } while(ProfileManager.Exists(name) || File.Exists(Path.Combine(Main.ProfilePath, name + ".json")));

            var config = new ProfileConfig {
                Name = name,
                Path = name + ".json"
            };

            var profile = ProfileManager.Create(config);
            if(profile != null) {
                ProfileManager.Save();
                dragSoltNeedInit = true;
            }
        }

        NeoDrawer.StaticInstance.UpdateFocused();
    }

    private void DrawMenuButton(string label, ExtraMenus menu) {
        var prev = extraMenu;
        if(Drawer.Button(label + (extraMenu == menu ? "▼" : "▲"))) {
            extraMenu = extraMenu == menu ? ExtraMenus.Closed : menu;
            if(extraMenu != prev) {
                NeoDrawer.StaticInstance.FieldClear();
            }
        }
    }

    private int egClickCount = 0;
    private DateTime egFirstClickTime = DateTime.MinValue;

    private void egHandle() {
        if(Main.EgEnabled) {
            return;
        }
        DateTime now = DateTime.UtcNow;

        if(egClickCount == 0) {
            egFirstClickTime = now;
        }

        egClickCount++;

        if((now - egFirstClickTime).TotalSeconds > 1.0) {
            egClickCount = 1;
            egFirstClickTime = now;
        }

        if(egClickCount >= 10) {
            Main.EgEnabled = true;
            egClickCount = 0;
        }
    }
}
