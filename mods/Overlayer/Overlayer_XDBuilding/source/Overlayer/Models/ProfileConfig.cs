using Newtonsoft.Json.Linq;
using Overlayer.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Overlayer.Models;

public class ProfileConfig : IModel, ICopyable<ProfileConfig> {
    public bool Active = true;
    public string Name = "Profile NULL";
    public string Path = null;
    public float Opacity = 1f;
    public List<ObjectConfig> Objects = [];
    public bool MigratedFromLegacyFormat { get; private set; } = false;

    public ProfileConfig Copy() {
        return new ProfileConfig {
            Active = Active,
            Name = Name,
            Path = Path,
            Opacity = Opacity,
            Objects = Objects?.Select(o => o.Copy()).ToList() ?? []
        };
    }

    public JToken Serialize() {
        var node = new JObject {
            [nameof(Active)] = Active,
            [nameof(Name)] = Name,
            [nameof(Opacity)] = Opacity,
            [nameof(Objects)] = new JArray(
                Objects.Select(o => {
                    var objNode = (JObject)o.Serialize();
                    var typeName = o.GetType().Name.Replace("Config", "");
                    var newNode = new JObject { ["Type"] = typeName };
                    foreach(var prop in objNode.Properties()) {
                        newNode[prop.Name] = prop.Value;
                    }
                    return newNode;
                })
            )
        };
        return node;
    }

    public void Deserialize(JToken node) {
        var defaults = new ProfileConfig();

        Active = defaults.Active;
        Opacity = defaults.Opacity;
        Objects = [];
        MigratedFromLegacyFormat = false;

        if(node == null) {
            return;
        }

        // Legacy object-list format: the whole file is directly [ TextConfig, TextConfig, ... ].
        // Older Overlayer exports and some shared configs used this before profile files became
        // { Active, Opacity, Objects: [...] }. Treat it as one active profile and preserve every
        // object's own Active flag.
        if(node is JArray legacyObjects) {
            Objects = DeserializeObjectList(legacyObjects);
            MigratedFromLegacyFormat = true;
            return;
        }

        if(node is not JObject objNode) {
            return;
        }

        // Single-object export format: { PlayingText: "...", Font: "...", ... } or
        // { Type: "Image", Images: [...] }. Import it as a one-object profile instead of
        // silently creating an empty/default profile.
        if(IsObjectConfigLike(objNode) && objNode[nameof(Objects)] == null && objNode["Texts"] == null) {
            var cfg = DeserializeObjectConfig(objNode);
            if(cfg != null) {
                Objects.Add(cfg);
            }
            MigratedFromLegacyFormat = true;
            return;
        }

        Active = objNode[nameof(Active)]?.Value<bool>() ?? defaults.Active;
        Name = objNode[nameof(Name)]?.Value<string>() ?? Name;
        Opacity = objNode[nameof(Opacity)]?.Value<float>() ?? defaults.Opacity;
        Objects = [];

        var objectTokens = objNode[nameof(Objects)] as JArray
            ?? objNode["Texts"] as JArray;

        if(objNode["Texts"] is JArray && objNode[nameof(Objects)] == null) {
            MigratedFromLegacyFormat = true;
        }

        if(objectTokens == null) {
            return;
        }

        Objects = DeserializeObjectList(objectTokens);
    }

    public static List<ObjectConfig> DeserializeObjectList(JArray objectTokens) {
        var objects = new List<ObjectConfig>();
        if(objectTokens == null) {
            return objects;
        }

        foreach(var obj in objectTokens) {
            var cfg = DeserializeObjectConfig(obj);
            if(cfg != null) {
                objects.Add(cfg);
            }
        }

        return objects;
    }

    public static ObjectConfig DeserializeObjectConfig(JToken obj) {
        if(obj == null || obj.Type != JTokenType.Object) {
            return null;
        }

        string typeName = InferObjectType(obj);
        ObjectConfig cfg = typeName switch {
            "Image" => new ImageConfig(),
            "Text" => new TextConfig(),
            _ => new TextConfig()
        };

        cfg.Deserialize(obj);
        return cfg;
    }

    public static string InferObjectType(JToken obj) {
        if(obj == null || obj.Type != JTokenType.Object) {
            return "Text";
        }

        string typeName = obj["Type"]?.Value<string>()?.Trim();
        if(!string.IsNullOrWhiteSpace(typeName)) {
            if(typeName.EndsWith("Config", StringComparison.OrdinalIgnoreCase)) {
                typeName = typeName.Substring(0, typeName.Length - "Config".Length);
            }

            return typeName switch {
                "Image" => "Image",
                "Text" => "Text",
                _ => typeName
            };
        }

        // Heuristics for legacy files with no Type field.
        if(obj["Images"] != null || obj["PlayingCommand"] != null || obj["NotPlayingCommand"] != null) {
            return "Image";
        }

        if(obj["PlayingText"] != null || obj["NotPlayingText"] != null || obj["Font"] != null || obj["FontSize"] != null) {
            return "Text";
        }

        return "Text";
    }

    public static bool IsObjectConfigLike(JToken obj) {
        if(obj == null || obj.Type != JTokenType.Object) {
            return false;
        }

        return obj["Type"] != null
            || obj["Images"] != null
            || obj["PlayingCommand"] != null
            || obj["NotPlayingCommand"] != null
            || obj["PlayingText"] != null
            || obj["NotPlayingText"] != null
            || obj["Font"] != null
            || obj["FontSize"] != null;
    }
}
