using Overlayer.Models;
using Overlayer.Unity;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Overlayer.Core;

public class ObjectManager {
    public int Count {
        get {
            CleanInvalid();
            return Objects.Count;
        }
    }

    public List<OverlayerObject> Objects = [];
    public OverlayerProfile ProfileCanvas;

    public ObjectManager(OverlayerProfile profileCanvas) => ProfileCanvas = profileCanvas;

    public void Create(ObjectConfig cfg) {
        switch(cfg) {
            case TextConfig t: {
                Create(t);
                break;
            }
            case ImageConfig i: {
                Create(i);
                break;
            }
        }
    }

    public OverlayerText Create(TextConfig config) {
        if(string.IsNullOrEmpty(config.Name)) {
            config.Name = $"Text {Count + 1}";
        }

        var go = new GameObject($"OverlayerText_{Count + 1}");
        var obj = go.AddComponent<OverlayerText>();
        obj.Init(ProfileCanvas, config);

        Objects.Add(obj);
        return obj;
    }

    public OverlayerImage Create(ImageConfig config) {
        if(string.IsNullOrEmpty(config.Name)) {
            config.Name = $"Image {Count + 1}";
        }

        var go = new GameObject($"OverlayerImage_{Count + 1}");
        var obj = go.AddComponent<OverlayerImage>();
        obj.Init(ProfileCanvas, config);

        Objects.Add(obj);
        return obj;
    }

    public OverlayerObject Get(int index) => (index >= 0 && index < Count) ? Objects[index] : null;

    public bool OrderToIndex(int from, int to) {
        CleanInvalid();
        if(from < 0 || from >= Count || to < 0 || to >= Count || from == to) {
            return false;
        }

        var item = Objects[from];
        if(item == null) {
            CleanInvalid();
            return false;
        }
        Objects.RemoveAt(from);
        Objects.Insert(to, item);

        item.gameObject.transform.SetSiblingIndex(to);

        return true;
    }
    public bool OrderUp(int index) => OrderToIndex(index, index - 1);
    public bool OrderDown(int index) => OrderToIndex(index, index + 1);
    public bool OrderToTop(int index) => OrderToIndex(index, 0);
    public bool OrderToBottom(int index) => OrderToIndex(index, Count - 1);
    public bool OrderByDrag(int fromIndex, int toIndex) {
        CleanInvalid();
        if(fromIndex < 0 || fromIndex >= Count) {
            return false;
        }

        toIndex = Mathf.Clamp(toIndex, 0, Count);

        if(fromIndex == toIndex || fromIndex == toIndex - 1) {
            return false;
        }

        var item = Objects[fromIndex];
        if(item == null) {
            CleanInvalid();
            return false;
        }
        Objects.RemoveAt(fromIndex);

        if(fromIndex < toIndex) {
            toIndex--;
        }

        Objects.Insert(toIndex, item);
        item.gameObject.transform.SetSiblingIndex(toIndex);

        return true;
    }

    public void Import(List<ObjectConfig> configs) {
        if(configs == null) {
            return;
        }

        foreach(var config in configs) {
            if(config is TextConfig t) {
                Create(t);
            } else if(config is ImageConfig img) {
                Create(img);
            }
        }

        Refresh();
    }

    public List<ObjectConfig> Export() {
        CleanInvalid();
        return Objects.Where(o => o != null).Select(o => o.Config).Where(c => c != null).ToList();
    }

    public void Destroy(OverlayerObject obj) {
        if(obj == null) {
            Objects.RemoveAll(o => o == null);
            Refresh();
            return;
        }

        Objects.Remove(obj);

        try {
            if(obj && obj.gameObject) {
                Object.Destroy(obj.gameObject);
            }
        } catch(System.Exception e) {
            Main.Logger?.Log("[ObjectManager] Failed to destroy object: " + e);
        }

        Refresh();
    }

    private void CleanInvalid() {
        Objects.RemoveAll(o => o == null);
    }

    public void SuspendReferences() {
        CleanInvalid();
        foreach(var o in Objects.ToArray()) {
            try {
                switch(o) {
                    case OverlayerText text:
                        text.PlayingReplacer?.Dispose();
                        text.NotPlayingReplacer?.Dispose();
                        break;
                    case OverlayerImage image:
                        image.PlayingReplacer?.Dispose();
                        image.NotPlayingReplacer?.Dispose();
                        break;
                }
            } catch(System.Exception e) {
                Main.Logger?.Log("[ObjectManager] Failed to suspend object references: " + e);
            }
        }

        if(TagManager.Initialized) {
            TagManager.UpdatePatch();
        }
    }

    public void Refresh() {
        CleanInvalid();

        if(ProfileCanvas?.Config != null && !ProfileCanvas.Config.Active) {
            SuspendReferences();
            return;
        }

        foreach(var o in Objects.ToArray()) {
            if(o == null) {
                continue;
            }

            try {
                o.ApplyConfig();
            } catch(System.Exception e) {
                Main.Logger?.Log("[ObjectManager] Failed to refresh object '" + (o.Config?.Name ?? "<unknown>") + "': " + e);
            }
        }
    }

    public void Release() {
        CleanInvalid();
        foreach(var o in Objects.ToArray()) {
            if(o) {
                Object.Destroy(o.gameObject);
            }
        }

        Objects.Clear();
    }
}