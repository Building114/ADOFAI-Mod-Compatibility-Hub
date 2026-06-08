using HarmonyLib;
using KeyViewer.Controllers;
using KeyViewer.Core;
using KeyViewer.Core.Input;
using KeyViewer.Core.TextReplacing;
using KeyViewer.Core.Translation;
using KeyViewer.Migration.V3;
using KeyViewer.Models;
using KeyViewer.Patches;
using KeyViewer.Unity;
using KeyViewer.Utils;
using KeyViewer.Views;
using Newtonsoft.Json.Linq;
using Overlayer.Core;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using UnityEngine;
using static UnityModManagerNet.UnityModManager;
using static UnityModManagerNet.UnityModManager.ModEntry;
using Object = UnityEngine.Object;

namespace KeyViewer;

public static class Main {
    public static bool IsEnabled { get; private set; }
    public static bool IsPlaying { get; private set; }
    public static Translator Lang { get; internal set; }
    public static ModEntry Mod { get; private set; }
    public static ModLogger Logger { get; private set; }
    public static Settings Settings { get; private set; }
    public static Dictionary<string, KeyManager> Managers { get; private set; }
    public static ModelDrawable<Profile> ListeningDrawer { get; internal set; }
    public static Harmony Harmony { get; private set; }
    public static GUIController GUI { get; private set; }
    public static HashSet<string> ToDeleteFiles { get; private set; }
    public static event Action OnManagersInitialized = delegate { };
    public static bool IsWindows { get; private set; }
    public static string ProfilePath;
    public static string Tooltip = "";
    public static void Load(ModEntry modEntry) {
        Mod = modEntry;
        ProfilePath = Path.Combine(Mod.Path, "profiles");
        Logger = modEntry.Logger;

        GUI = new GUIController();
        Lang = new Translator("0KTL_KEYVIEWER");

        modEntry.OnToggle = OnToggle;
        modEntry.OnUpdate = OnUpdate;
        modEntry.OnGUI = OnGUI;
        modEntry.OnSaveGUI = OnSaveGUI;
        modEntry.OnShowGUI = OnShowGUI;
        modEntry.OnHideGUI = OnHideGUI;
        IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }
    public static bool OnToggle(ModEntry modEntry, bool toggle) {
        if(toggle) {
            Settings = new Settings();
            if(File.Exists(Constants.SettingsPath)) {
                var json = JToken.Parse(File.ReadAllText(Constants.SettingsPath));
                Settings.Deserialize(json);
            }

            if(IsWindows && Settings.UseWindowsAsyncInput) {
                WinInput.StartPolling(Settings.PollingRate);
            }

            WinInput.OnKeyDown += code => {
                KeyCode k = WinInput.IntToKeyCode(code);
                if(k != KeyCode.None) {
                    MainThreadDispatcher.Enqueue(() => {
                        ListeningDrawer?.OnKeyDown(k);
                    });
                }
            };

            Tag.InitializeWrapperAssembly();
            FontManager.Initialize();
            AssetManager.Initialize();

            Managers = new Dictionary<string, KeyManager>(StringComparer.OrdinalIgnoreCase);
            ToDeleteFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if(!Directory.Exists(ProfilePath)) {
                Directory.CreateDirectory(ProfilePath);
            }

            NormalizeActiveProfiles();

            var profileFiles = Directory.GetFiles(ProfilePath, "*.json");
            var profileNames = new HashSet<string>(
                profileFiles.Select(Path.GetFileNameWithoutExtension),
                StringComparer.OrdinalIgnoreCase
            );

            // Settings may contain stale rows for files that no longer exist.
            Settings.ActiveProfiles.RemoveAll(p => !profileNames.Contains(p.Name));

            List<string> failedProfiles = [];
            foreach(var file in profileFiles) {
                var profileName = Path.GetFileNameWithoutExtension(file);
                try {
                    int existingIndex = Settings.ActiveProfiles.FindIndex(
                        p => string.Equals(p.Name, profileName, StringComparison.OrdinalIgnoreCase)
                    );

                    ActiveProfile activeProfile;
                    if(existingIndex >= 0) {
                        activeProfile = Settings.ActiveProfiles[existingIndex];
                        // Keep the on-disk file name as the canonical spelling.
                        activeProfile.Name = profileName;
                        Settings.ActiveProfiles[existingIndex] = activeProfile;
                    } else {
                        // A valid profile file not yet present in Settings is an imported profile,
                        // so default it to enabled instead of silently hiding it after restart.
                        activeProfile = new ActiveProfile(profileName, true);
                        Settings.ActiveProfiles.Add(activeProfile);
                    }

                    if(!AddManager(activeProfile)) {
                        failedProfiles.Add(profileName);
                    }
                } catch(Exception ex) {
                    Logger.Log($"Failed to load profile {file}: {ex}");
                    failedProfiles.Add(profileName);
                }
            }

            Settings.ActiveProfiles.RemoveAll(
                p => failedProfiles.Any(name => string.Equals(name, p.Name, StringComparison.OrdinalIgnoreCase))
            );
            NormalizeActiveProfiles();
            SaveSettingsNow();

            Lang.Language = Settings.Lang;
            Lang.OnInitialize += OnLanguageInitialize;
            var settingsDrawer = new SettingsDrawer(Settings);
            Lang.OnInitialize += () => settingsDrawer.NeedLangInit = true;
            _ = Lang.Load(Path.Combine(Mod.Path, "lang"));

            Harmony = new Harmony(modEntry.Info.Id);
            Harmony.PatchAll(Assembly.GetExecutingAssembly());
            GameLifecyclePatch.Apply(Harmony);
            StaticCoroutine.Run(InitializeManagersCo());

            DllImporter.NCalcInitialize();

            GUI.Init(settingsDrawer);
            GUI.Flush();

            ListeningDrawer = null;
            IsEnabled = true;
        } else {
            IsEnabled = false;
            ReleaseManagers();
            ToDeleteFiles = null;
            Harmony.UnpatchAll(Harmony.Id);
            Harmony = null;
            if(IsWindows) {
                WinInput.Dispose();
            }
            // AssetManager.Release();
            FontManager.Release();
            Tag.ReleaseWrapperAssembly();
            Resources.UnloadUnusedAssets();
            Lang.Release();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        }
        return true;
    }
    public static class MainThreadDispatcher {
        private static readonly ConcurrentQueue<Action> queue = new();
        public static void Enqueue(Action action) {
            queue.Enqueue(action);
        }
        public static void Update() {
            while(queue.TryDequeue(out var action)) {
                action.Invoke();
            }
        }
    }
    public static void OnUpdate(ModEntry modEntry, float deltaTime) {
        IsPlaying = GetIsPlayingSafe();

        if(ListeningDrawer != null && (!IsWindows || !Settings.UseWindowsAsyncInput)) {
            foreach(KeyCode code in Enum.GetValues(typeof(KeyCode))) {
                if(KeyInput.GetKeyDown(code)) {
                    ListeningDrawer.OnKeyDown(code);
                }
            }
        }

        UpdateManagerVisibility();
        MainThreadDispatcher.Update();
    }

    private static bool GetIsPlayingSafe() {
        try {
            object controller = scrController.instance;
            if(controller == null) return false;

            if(GetBoolMember(controller, "paused", false)) return false;

            object conductor = scrConductor.instance;
            if(conductor != null) {
                if(GetBoolMember(conductor, "isGameWorld", false)) return true;
                if(GetBoolMember(conductor, "gameworld", false)) return true;
            }

            return IsPlaying;
        } catch(Exception ex) {
            Logger?.Log($"Safe play-state check failed: {ex.GetType().Name}: {ex.Message}");
            return IsPlaying;
        }
    }

    private static bool GetBoolMember(object instance, string name, bool fallback) {
        if(instance == null) return fallback;

        Type type = instance.GetType();
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        FieldInfo field = type.GetField(name, flags);
        if(field != null && field.FieldType == typeof(bool)) return (bool)field.GetValue(instance);

        PropertyInfo property = type.GetProperty(name, flags);
        if(property != null && property.PropertyType == typeof(bool)) return (bool)property.GetValue(instance);

        return fallback;
    }

    private static void UpdateManagerVisibility() {
        if(Managers == null) return;

        var activeNames = new HashSet<string>(
            Settings?.ActiveProfiles?.Where(p => p.Active)
                .Select(p => p.Name)
            ?? Enumerable.Empty<string>(),
            StringComparer.OrdinalIgnoreCase
        );

        foreach(var (name, manager) in Managers.ToArray()) {
            if(manager == null || manager.gameObject == null) {
                Managers.Remove(name);
                continue;
            }

            bool profileEnabled = activeNames.Contains(name);
            bool showViewer = profileEnabled && (!manager.profile.ViewOnlyGamePlay || IsPlaying);
            if(showViewer != manager.gameObject.activeSelf) {
                manager.gameObject.SetActive(showViewer);
            }
        }
    }

    public static void MarkGameplayStarted() {
        IsPlaying = true;
        UpdateManagerVisibility();
    }

    public static void MarkGameplayStopped() {
        IsPlaying = false;
        ResetKeys();
        UpdateManagerVisibility();
    }

    public static void MarkGameplayEnded() {
        IsPlaying = false;
        ResetKeys();
        UpdateManagerVisibility();
    }
    public static void OnGUI(ModEntry modEntry) {
        GUI.Draw();
        if(Settings.UseTooltip) {
            Drawer.Tooltip(Tooltip);
            Tooltip = null;
        }
    }
    public static void OnSaveGUI(ModEntry modEntry) {
        SaveAllNow();

        if(ToDeleteFiles == null) return;
        foreach(var path in ToDeleteFiles.ToArray()) {
            if(File.Exists(path)) {
                File.Delete(path);
            }
        }
        ToDeleteFiles.Clear();
    }

    public static void NormalizeActiveProfiles() {
        if(Settings == null) return;

        Settings.ActiveProfiles ??= [];
        Settings.ActiveProfiles = Settings.ActiveProfiles
            .Where(p => !string.IsNullOrWhiteSpace(p.Name)
                && !string.Equals(p.Name, "None", StringComparison.OrdinalIgnoreCase))
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ActiveProfile(group.First().Name, group.Any(p => p.Active)))
            .ToList();
    }

    public static void SaveSettingsNow() {
        if(Settings == null) return;

        NormalizeActiveProfiles();
        File.WriteAllText(Constants.SettingsPath, Settings.Serialize().ToString());
    }

    public static bool SaveProfileNow(string name, Profile profile) {
        if(string.IsNullOrWhiteSpace(name) || profile == null) return false;

        try {
            File.WriteAllText(
                Path.Combine(ProfilePath, $"{name}.json"),
                profile.Serialize().ToString()
            );
            return true;
        } catch(Exception ex) {
            Logger?.Log($"Failed to save profile {name}: {ex}");
            return false;
        }
    }

    public static void SaveAllNow() {
        SaveSettingsNow();
        if(Managers == null) return;

        foreach(var (name, manager) in Managers.ToArray()) {
            if(manager == null || manager.profile == null) continue;
            SaveProfileNow(name, manager.profile);
        }
    }
    public static void OnShowGUI(ModEntry modEntry) {
        GUI.Flush();
        ListeningDrawer = null;
    }
    public static void OnHideGUI(ModEntry modEntry) {
        GUI.Flush();
        ListeningDrawer = null;
    }
    public static void OnLanguageInitialize() {
        string[] translatorLogs = Lang.Logs;
        if(translatorLogs != null && translatorLogs.Length > 0) {
            foreach(var log in translatorLogs) {
                Logger.Log(log);
            }
        }
    }
    public static bool AddManager(ActiveProfile profile, bool forceInit = false) {
        if(Managers == null || string.IsNullOrWhiteSpace(profile.Name)) return false;

        var profilePath = Path.Combine(ProfilePath, $"{profile.Name}.json");
        if(!File.Exists(profilePath)) return false;
        if(!profile.Active) return true;

        try {
            if(Managers.TryGetValue(profile.Name, out var existingManager)) {
                if(existingManager != null && existingManager.gameObject != null) {
                    if(forceInit && !existingManager.initialized) {
                        existingManager.Init();
                        existingManager.UpdateKeys();
                        Logger.Log($"Initialized Key Manager {profile.Name}.");
                    }
                    UpdateManagerVisibility();
                    return true;
                }

                Managers.Remove(profile.Name);
            }

            var profileJson = JToken.Parse(File.ReadAllText(profilePath));
            var importedProfile = ProfileImporter.Import(profileJson);
            if(importedProfile == null) {
                throw new InvalidDataException("Profile importer returned null.");
            }

            var newManager = KeyManager.CreateManager(profile.Name, importedProfile);
            if(newManager == null) {
                throw new InvalidOperationException("KeyManager.CreateManager returned null.");
            }

            if(forceInit) {
                newManager.Init();
                newManager.UpdateKeys();
            }

            Managers[profile.Name] = newManager;
            UpdateManagerVisibility();

            if(forceInit) {
                Logger.Log($"Initialized Key Manager {profile.Name}.");
            }
            return true;
        } catch(Exception ex) {
            Logger?.Log($"Failed to create Key Manager {profile.Name}: {ex}");
            return false;
        }
    }

    public static bool TryGetManager(ActiveProfile profile, bool repair, out KeyManager manager) {
        manager = null;
        if(Managers != null
            && Managers.TryGetValue(profile.Name, out manager)
            && manager != null
            && manager.gameObject != null) {
            return true;
        }

        if(!repair) return false;

        // Loading an inactive profile for export is safe: visibility is still controlled
        // by Settings.ActiveProfiles and UpdateManagerVisibility keeps it hidden.
        var loadProfile = profile;
        loadProfile.Active = true;
        if(!AddManager(loadProfile, true)) return false;

        return Managers.TryGetValue(profile.Name, out manager)
            && manager != null
            && manager.gameObject != null;
    }

    public static bool SetManagerActive(ActiveProfile profile) {
        if(profile.Active) {
            if(!TryGetManager(profile, true, out _)) {
                return false;
            }
        }

        // Showing/hiding only changes Settings.ActiveProfiles. It must not rewrite
        // the profile payload, especially during rapid toggles.
        UpdateManagerVisibility();
        return true;
    }

    public static void RemoveManager(ActiveProfile profile, bool saveProfile = true) {
        if(Managers == null) return;

        if(Managers.TryGetValue(profile.Name, out var manager)) {
            if(manager != null) {
                if(saveProfile) {
                    SaveProfileNow(profile.Name, manager.profile);
                }
                manager.Dispose();
                if(manager.gameObject != null) {
                    manager.gameObject.SetActive(false);
                    Object.Destroy(manager.gameObject);
                }
            }

            Managers.Remove(profile.Name);
            Logger.Log($"Released Key Manager {profile.Name}.");
        }
    }
    public static (KeyManager manager, ActiveProfile activeProfile) CreateManagerImmediate(string name, Profile p, string key = null) {
        var profile = new ActiveProfile(name, true, key);

        if(Managers.TryGetValue(name, out var oldManager) && oldManager != null) {
            oldManager.Dispose();
            if(oldManager.gameObject != null) {
                oldManager.gameObject.SetActive(false);
                Object.Destroy(oldManager.gameObject);
            }
        }

        var manager = KeyManager.CreateManager(profile.Name, p);
        manager.Init();
        manager.UpdateKeys();
        Managers[name] = manager;
        UpdateManagerVisibility();
        Logger.Log($"Initialized Key Manager {profile.Name}.");
        return (manager, profile);
    }
    public static IEnumerator InitializeManagersCo() {
        if(!AssetManager.Initialized) {
            yield return new WaitUntil(() => AssetManager.Initialized);
        }

        foreach(var (name, manager) in Managers.ToArray()) {
            if(manager == null || manager.gameObject == null) continue;

            try {
                var elapsed = MiscUtils.MeasureTime(() => {
                    manager.Init();
                    manager.UpdateKeys();
                });
                Logger.Log($"Initialized Key Manager {name}. ({elapsed.TotalMilliseconds}ms)");
            } catch(Exception ex) {
                Logger.Log($"Failed to initialize Key Manager {name}: {ex}");
                RemoveManager(new ActiveProfile(name, true), false);
            }
            yield return null;
        }

        UpdateManagerVisibility();
        OnManagersInitialized();
        yield break;
    }
    public static void ReleaseManagers() {
        if(Managers == null) return;

        foreach(var (name, manager) in Managers.ToArray()) {
            if(manager != null) {
                manager.Dispose();
                if(manager.gameObject != null) {
                    manager.gameObject.SetActive(false);
                    Object.Destroy(manager.gameObject);
                }
            }
            Logger.Log($"Released Key Manager {name}.");
        }

        Managers.Clear();
        Managers = null;
    }
    public static void ResetKeys() {
        if(Managers == null) return;

        foreach(var manager in Managers.Values) {
            if(manager?.keys == null) continue;

            foreach(var key in manager.keys) {
                if(!key) continue;

                key.Pressed = false;
                key.ResetRains();
            }
        }
    }
    public static void MigrateFromV3Xml(string path) {
        XmlSerializer serializer;
        try {
            serializer = new XmlSerializer(typeof(V3Settings), GetXAO(true));
            var v3s = serializer.Deserialize(File.OpenRead(path)) as V3Settings;
            var newSettings = V3Migrator.Migrate(v3s, out var profilesNode);
            foreach(var (name, manager) in Managers) {
                Object.Destroy(manager.gameObject);
                Logger.Log($"Released Key Manager {name}.");
            }
            Managers.Clear();
            for(int i = 0; i < newSettings.ActiveProfiles.Count; i++) {
                var profile = newSettings.ActiveProfiles[i];
                File.WriteAllText(Path.Combine(ProfilePath, $"{profile.Name}.json"), profilesNode[i].ToString());
                AddManager(profile, true);
            }
            GUI.Flush();
            GUI.Init(new SettingsDrawer(Settings = newSettings));
            Logger.Log($"Successfully Migrated Settings Xml '{path}'");
        } catch(Exception e) {
            try {
                serializer = new XmlSerializer(typeof(V3Profile), GetXAO(false));
                var v3p = serializer.Deserialize(File.OpenRead(path)) as V3Profile;
                var profile = V3Migrator.MigrateProfile(v3p);
                File.WriteAllText(Path.Combine(ProfilePath, $"{v3p.Name}.json"), profile.Serialize().ToString());
                var activeProfile = new ActiveProfile(v3p.Name, true);
                Settings.ActiveProfiles.Add(activeProfile);
                AddManager(activeProfile, true);
                Logger.Log($"Successfully Migrated Profile Xml '{path}'");
            } catch(Exception ee) { Logger.Log($"Failed To Migrate Xml..\n{e}\n\n{ee}"); }
        }
    }
    public static V3Settings ReadV3Settings(string path) {
        var serializer = new XmlSerializer(typeof(V3Settings), GetXAO(true));
        return serializer.Deserialize(File.OpenRead(path)) as V3Settings;
    }
    public static V3Profile ReadV3Profile(string path) {
        var serializer = new XmlSerializer(typeof(V3Profile), GetXAO(true));
        return serializer.Deserialize(File.OpenRead(path)) as V3Profile;
    }
    private static XmlAttributeOverrides GetXAO(bool settings) {
        XmlAttributeOverrides xao = new();

        if(settings) {
            XmlAttributes settingsAttr = new() {
                XmlRoot = new XmlRootAttribute("Settings")
            };
            xao.Add(typeof(V3Settings), settingsAttr);
        } else {
            XmlAttributes profileAttr = new() {
                XmlRoot = new XmlRootAttribute("Profile")
            };
            xao.Add(typeof(V3Profile), profileAttr);
        }

        return xao;
    }
}
