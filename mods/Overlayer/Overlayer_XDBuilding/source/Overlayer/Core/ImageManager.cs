using Overlayer.Unity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Overlayer.Core;

public static class ImageManager {
    public enum ImageLoadState {
        Loaded,
        Empty,
        Missing,
        Invalid
    }

    public static bool Initialized { get; private set; } = false;
    public static Sprite DefaultSprite {
        get {
            if(_defaultSprite is null) {
                CreateDefault();
            }
            return _defaultSprite;
        }

        private set => _defaultSprite = value;
    }
    static Sprite _defaultSprite;
    private static Dictionary<string, Sprite> Sprites;
    private static List<Sprite> RetiredSprites;
    private static HashSet<string> ReportedFailures;
    private static HashSet<string> ManualSprites;
    private static HashSet<Sprite> OwnedSprites;

    static void CreateDefault() {
        Texture2D tex = new(2, 2);
        tex.SetPixels([Color.clear, Color.clear, Color.clear, Color.clear]);
        tex.Apply();
        _defaultSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
    }

    public static Sprite GetSpriteSafe(string name) => TryGetSprite(name, out Sprite sprite) ? sprite : DefaultSprite;
    public static Sprite GetSprite(string name) => TryGetSprite(name, out Sprite sprite) ? sprite : null;

    public static void SetSprite(string name, Sprite sprite) {
        if(string.IsNullOrWhiteSpace(name)) {
            return;
        }
        if(!Initialized) {
            Initialize();
        }
        string path = name.Replace("{ModDir}", Main.Mod.Path);
        if(Sprites.TryGetValue(path, out Sprite previous) && previous != sprite) {
            RetiredSprites.Add(previous);
        }
        Sprites[path] = sprite;
        ManualSprites.Add(path);
    }

    public static bool TryGetSprite(string name, out Sprite sprite) => TryGetSprite(name, out sprite, out _);

    public static bool TryGetSprite(string name, out Sprite sprite, out ImageLoadState state) {
        sprite = DefaultSprite;
        if(string.IsNullOrWhiteSpace(name)) {
            state = ImageLoadState.Empty;
            return false;
        }
        if(!Initialized) {
            Initialize();
        }

        string path = name.Replace("{ModDir}", Main.Mod.Path);
        if(ManualSprites.Contains(path) && Sprites.TryGetValue(path, out Sprite manual) && manual) {
            sprite = manual;
            state = ImageLoadState.Loaded;
            return true;
        }
        if(!File.Exists(path)) {
            Retire(path);
            state = ImageLoadState.Missing;
            return false;
        }
        if(Sprites.TryGetValue(path, out Sprite cached)) {
            if(cached) {
                sprite = cached;
                state = ImageLoadState.Loaded;
                return true;
            }
            Sprites.Remove(path);
        }

        Texture2D tex = null;
        try {
            byte[] bytes = File.ReadAllBytes(path);
            tex = new Texture2D(2, 2);
            if(!tex.LoadImage(bytes)) {
                state = ImageLoadState.Invalid;
                ReportFailure(path, "Invalid image data");
                return false;
            }
            Sprite loaded = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            if(!loaded) {
                state = ImageLoadState.Invalid;
                ReportFailure(path, "Unable to create sprite");
                return false;
            }
            OwnedSprites.Add(loaded);
            Sprites[path] = loaded;
            sprite = loaded;
            tex = null;
            state = ImageLoadState.Loaded;
            ReportedFailures.Remove(path);
            return true;
        } catch(Exception e) {
            state = ImageLoadState.Invalid;
            ReportFailure(path, e.Message);
            return false;
        } finally {
            if(tex) {
                UnityEngine.Object.Destroy(tex);
            }
        }
    }

    private static void ReportFailure(string path, string reason) {
        if(ReportedFailures.Add(path)) {
            Main.Logger?.Log($"[ImageManager] Unable to load image '{path}': {reason}");
        }
    }

    private static void Retire(string path) {
        ManualSprites.Remove(path);
        if(Sprites.TryGetValue(path, out Sprite old)) {
            Sprites.Remove(path);
            if(old) {
                RetiredSprites.Add(old);
            }
        }
    }

    public static void Invalidate(string name) {
        if(string.IsNullOrWhiteSpace(name) || !Initialized) {
            return;
        }
        string path = name.Replace("{ModDir}", Main.Mod.Path);
        Retire(path);
        ReportedFailures.Remove(path);
    }

    private static void DestroySprite(Sprite sprite) {
        if(!sprite || sprite == _defaultSprite || OwnedSprites == null || !OwnedSprites.Remove(sprite)) {
            return;
        }
        if(sprite.texture) {
            UnityEngine.Object.Destroy(sprite.texture);
        }
        UnityEngine.Object.Destroy(sprite);
    }

    public static void CleanUp() {
        if(!Initialized) {
            return;
        }
        HashSet<Sprite> inUse = new(Sprites.Values.Where(sprite => sprite));
        if(ProfileManager.Profiles != null) {
            foreach(var profile in ProfileManager.Profiles) {
                if(profile?.ObjectManager?.Objects == null) {
                    continue;
                }
                foreach(var obj in profile.ObjectManager.Objects) {
                    if(obj is OverlayerImage image) {
                        foreach(var sp in image.Images) {
                            if(sp) {
                                inUse.Add(sp);
                            }
                        }
                    }
                }
            }
        }
        for(int i = RetiredSprites.Count - 1; i >= 0; i--) {
            if(!inUse.Contains(RetiredSprites[i])) {
                DestroySprite(RetiredSprites[i]);
                RetiredSprites.RemoveAt(i);
            }
        }
    }

    public static void Initialize() {
        if(Initialized) {
            return;
        }
        Sprites = [];
        RetiredSprites = [];
        ReportedFailures = [];
        ManualSprites = [];
        OwnedSprites = [];
        Initialized = true;
    }

    public static void Release() {
        if(!Initialized) {
            return;
        }
        foreach(var sp in OwnedSprites.ToArray()) {
            DestroySprite(sp);
        }
        OwnedSprites.Clear();
        OwnedSprites = null;
        Sprites.Clear();
        Sprites = null;
        RetiredSprites.Clear();
        RetiredSprites = null;
        ReportedFailures.Clear();
        ReportedFailures = null;
        ManualSprites.Clear();
        ManualSprites = null;

        if(_defaultSprite != null) {
            if(_defaultSprite.texture != null) {
                UnityEngine.Object.Destroy(_defaultSprite.texture);
            }
            UnityEngine.Object.Destroy(_defaultSprite);
            DefaultSprite = null;
        }

        Initialized = false;
    }
}
