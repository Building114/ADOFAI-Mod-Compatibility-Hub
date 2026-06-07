using Newtonsoft.Json.Linq;
using Overlayer.Models;
using Overlayer.Unity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Overlayer.Core;

public static class ProfileManager {
    public static bool Initialized { get; private set; }
    public static List<OverlayerProfile> Profiles;
    public static int Count => Profiles?.Count ?? 0;

    public static void Initialize() {
        if(Initialized) {
            return;
        }

        Profiles = [];
        Directory.CreateDirectory(Main.ProfilePath);

        bool anyMigrated = false;

        foreach(var file in Directory.GetFiles(Main.ProfilePath, "*.json")) {
            try {
                var content = File.ReadAllText(file);
                if(string.IsNullOrWhiteSpace(content)) {
                    continue;
                }

                var token = JToken.Parse(content);
                var cfg = new ProfileConfig();
                cfg.Deserialize(token);
                cfg.Path = file;
                cfg.Name = Path.GetFileNameWithoutExtension(file);

                if(cfg.MigratedFromLegacyFormat) {
                    anyMigrated = true;
                    TryBackupLegacyProfile(file);
                    Main.Logger?.Log($"[ProfileManager] Migrated legacy profile JSON '{file}' to the 3.49 profile format.");
                }

                var profile = CreateRuntimeProfile(cfg);
                Profiles.Add(profile);
            } catch(Exception e) {
                Main.Logger?.Log($"[ProfileManager] Failed to load profile '{file}': {e}");
            }
        }

        Initialized = true;

        if(anyMigrated) {
            Save();
        }

        if(TagManager.Initialized) {
            TagManager.UpdatePatch();
        }
    }

    private static void TryBackupLegacyProfile(string file) {
        try {
            if(string.IsNullOrWhiteSpace(file) || !File.Exists(file)) {
                return;
            }

            string backup = file + ".legacy.bak";
            if(!File.Exists(backup)) {
                File.Copy(file, backup, false);
            }
        } catch(Exception e) {
            Main.Logger?.Log("[ProfileManager] Failed to backup legacy profile '" + file + "': " + e);
        }
    }

    public static OverlayerProfile Create(ProfileConfig config) {
        if(config == null || string.IsNullOrWhiteSpace(config.Name) || Exists(config.Name)) {
            Debug.LogWarning("Profile name is invalid or already exists.");
            return null;
        }

        var cfg = new ProfileConfig {
            Active = config.Active,
            Name = config.Name,
            Path = Path.Combine(Main.ProfilePath, config.Name + ".json"),
            Opacity = config.Opacity,
            Objects = config.Objects?.Select(o => o.Copy()).ToList() ?? []
        };

        var profile = CreateRuntimeProfile(cfg);
        Profiles.Add(profile);
        if(TagManager.Initialized) {
            TagManager.UpdatePatch();
        }

        return profile;
    }

    public static OverlayerProfile CreateRuntimeProfile(ProfileConfig cfg) {
        if(cfg == null) {
            return null;
        }

        var profileGO = new GameObject(cfg.Name ?? "Profile");
        var profile = profileGO.AddComponent<OverlayerProfile>();
        profile.Config = cfg;
        profile.Init(cfg.Name);
        profile.ObjectManager.Import(cfg.Objects);
        profile.ApplyConfig();
        return profile;
    }

    public static OverlayerProfile Get(int index) {
        CleanInvalid();
        return (index >= 0 && index < Count) ? Profiles[index] : null;
    }

    private static void CleanInvalid() {
        Profiles?.RemoveAll(p => p == null);
    }

    public static bool OrderToIndex(int from, int to) {
        CleanInvalid();
        if(from < 0 || from >= Count || to < 0 || to >= Count || from == to) {
            return false;
        }

        var item = Profiles[from];
        Profiles.RemoveAt(from);
        Profiles.Insert(to, item);

        item.gameObject.transform.SetSiblingIndex(to);

        return true;
    }
    public static bool OrderUp(int index) => OrderToIndex(index, index - 1);
    public static bool OrderDown(int index) => OrderToIndex(index, index + 1);
    public static bool OrderToTop(int index) => OrderToIndex(index, 0);
    public static bool OrderToBottom(int index) => OrderToIndex(index, Count - 1);
    public static bool OrderByDrag(int from, int to) {
        CleanInvalid();
        if(from < 0 || from >= Count) {
            return false;
        }

        to = Mathf.Clamp(to, 0, Count);

        if(from == to || from == to - 1) {
            return false;
        }

        var item = Profiles[from];
        Profiles.RemoveAt(from);

        if(from < to) {
            to--;
        }

        Profiles.Insert(to, item);
        item.gameObject.transform.SetSiblingIndex(to);

        return true;
    }

    public static void Destroy(OverlayerProfile profile) {
        if(profile is null) {
            CleanInvalid();
            return;
        }

        try {
            string filePath = profile.Config?.Path ?? string.Empty;
            if(!string.IsNullOrEmpty(filePath) && !Path.IsPathRooted(filePath)) {
                filePath = Path.Combine(Main.ProfilePath, filePath);
            }
            if(File.Exists(filePath)) {
                File.Delete(filePath);
            }
        } catch(Exception e) {
            Main.Logger?.Log("[ProfileManager] Failed to delete profile file: " + e);
        }

        Profiles?.Remove(profile);

        try {
            profile.ObjectManager?.Release();
        } catch(Exception e) {
            Main.Logger?.Log("[ProfileManager] Failed to release profile objects: " + e);
        }

        if(profile.gameObject) {
            UnityEngine.Object.Destroy(profile.gameObject);
        }
        if(TagManager.Initialized) {
            TagManager.UpdatePatch();
        }
    }

    public static void Save() {
        if(Profiles == null) {
            return;
        }

        CleanInvalid();
        Directory.CreateDirectory(Main.ProfilePath);

        foreach(var profile in Profiles.ToArray()) {
            if(profile == null || profile.Config == null) {
                continue;
            }

            try {
                profile.Config.Objects = profile.ObjectManager?.Export() ?? [];
                JToken jsonNode = profile.Config.Serialize();

                string filePath = profile.Config.Path;
                if(string.IsNullOrEmpty(filePath)) {
                    filePath = Path.Combine(Main.ProfilePath, profile.Config.Name + ".json");
                    profile.Config.Path = filePath;
                } else if(!Path.IsPathRooted(filePath)) {
                    filePath = Path.Combine(Main.ProfilePath, filePath);
                    profile.Config.Path = filePath;
                }

                string dir = Path.GetDirectoryName(filePath);
                if(!string.IsNullOrEmpty(dir)) {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(filePath, jsonNode.ToString(Newtonsoft.Json.Formatting.Indented));
            } catch(Exception e) {
                Main.Logger?.Log("[ProfileManager] Failed to save profile '" + (profile.Config?.Name ?? "<unknown>") + "': " + e);
            }
        }
    }

    public static bool Exists(string name) {
        CleanInvalid();
        return Profiles.Any(p => p?.Config != null && string.Equals(p.Config.Name, name, StringComparison.Ordinal));
    }

    public static bool Rename(OverlayerProfile profile, string newName) {
        if(profile == null || string.IsNullOrWhiteSpace(newName) || Profiles.Any(p => p != profile && string.Equals(p.Config.Name, newName, StringComparison.OrdinalIgnoreCase))) {
            return false;
        }

        try {
            string oldPath = profile.Config.Path;
            if(string.IsNullOrEmpty(oldPath)) {
                oldPath = Path.Combine(Main.ProfilePath, profile.Config.Name + ".json");
            } else if(!Path.IsPathRooted(oldPath)) {
                oldPath = Path.Combine(Main.ProfilePath, oldPath);
            }

            string newPath = Path.Combine(Main.ProfilePath, newName + ".json");

            if(File.Exists(oldPath)) {
                File.Move(oldPath, newPath);
            }

            profile.Config.Name = newName;
            profile.Config.Path = newPath;
            profile.gameObject.name = newName;
            return true;
        } catch(Exception e) {
            Debug.LogError("Failed to rename profile: " + e);
            return false;
        }
    }

    public static void Refresh() {
        if(Profiles == null) {
            return;
        }

        CleanInvalid();
        foreach(var profile in Profiles.ToArray()) {
            try {
                profile?.ApplyConfig();
                profile?.ObjectManager?.Refresh();
            } catch(Exception e) {
                Main.Logger?.Log("[ProfileManager] Failed to refresh profile '" + (profile?.Config?.Name ?? "<unknown>") + "': " + e);
            }
        }
        if(TagManager.Initialized) {
            TagManager.UpdatePatch();
        }
    }

    public static void Release() {
        if(!Initialized) {
            return;
        }

        Save();

        if(Profiles != null) {
            foreach(var profile in Profiles.ToArray()) {
                try {
                    profile?.ObjectManager?.Release();
                    if(profile != null && profile.gameObject) {
                        UnityEngine.Object.Destroy(profile.gameObject);
                    }
                } catch(Exception e) {
                    Main.Logger?.Log("[ProfileManager] Failed to release profile: " + e);
                }
            }
            Profiles.Clear();
        }

        OverlayerProfile.ReleaseStatics();
        Initialized = false;
    }
}