using Newtonsoft.Json.Linq;
using Overlayer.Core.Interfaces;
using Overlayer.Utils;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Overlayer.Core;

public class Reference : IModel, ICopyable<Reference> {
    public enum Type {
        Font,
        Image,
    }

    public Type ReferenceType;
    public string From;
    public string Name;
    public byte[] Raw;

    public static Reference GetReference(string path, Type referenceType) {
        if(string.IsNullOrWhiteSpace(path)) {
            return null;
        }
        string target = path.Replace("{ModDir}", Main.Mod.Path);
        try {
            if(!File.Exists(target)) {
                return null;
            }

            return new Reference {
                From = path,
                Name = Path.GetFileName(target),
                ReferenceType = referenceType,
                Raw = File.ReadAllBytes(target).Compress()
            };
        } catch(Exception e) {
            Main.Logger?.Log("[Reference] Failed to read file '" + target + "': " + e.Message);
            return null;
        }
    }

    public static bool MatchesPath(string path, Reference reference, bool allowNameFallback) {
        if(string.IsNullOrWhiteSpace(path) || reference == null) {
            return false;
        }
        if(!string.IsNullOrWhiteSpace(reference.From) &&
           string.Equals(path.Replace('\\', '/'), reference.From.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)) {
            return true;
        }
        return allowNameFallback && string.Equals(
            Path.GetFileName(path.Replace("{ModDir}", Main.Mod.Path)),
            reference.Name, StringComparison.OrdinalIgnoreCase);
    }

    public static string StoreEmbedded(string directory, string name, byte[] data) {
        string target = Path.Combine(directory, name);
        if(File.Exists(target) && !File.ReadAllBytes(target).SequenceEqual(data)) {
            string hash;
            using(var sha = SHA256.Create()) {
                hash = BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").Substring(0, 16);
            }
            target = Path.Combine(directory, Path.GetFileNameWithoutExtension(name) + "_" + hash + Path.GetExtension(name));
            if(File.Exists(target) && !File.ReadAllBytes(target).SequenceEqual(data)) {
                throw new IOException("Embedded reference filename collision: " + name);
            }
        }
        if(!File.Exists(target)) {
            Directory.CreateDirectory(directory);
            string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                File.WriteAllBytes(temporary, data);
                File.Move(temporary, target);
            } finally {
                if(File.Exists(temporary)) {
                    File.Delete(temporary);
                }
            }
        }
        return target;
    }

    public static void Flush() { }

    public JToken Serialize() {
        var node = new JObject {
            [nameof(ReferenceType)] = ReferenceType.ToString(),
            [nameof(From)] = From,
            [nameof(Name)] = Name,
            [nameof(Raw)] = Raw != null ? Convert.ToBase64String(Raw) : null
        };
        return node;
    }

    public void Deserialize(JToken node) {
        var obj = (JObject)node;
        ReferenceType = EnumHelper<Type>.Parse(obj[nameof(ReferenceType)]?.ToString());
        From = obj[nameof(From)]?.ToString();
        Name = obj[nameof(Name)]?.ToString();
        var rawStr = obj[nameof(Raw)]?.ToString();
        Raw = rawStr != null ? Convert.FromBase64String(rawStr) : null;
    }

    public Reference Copy() {
        return new Reference {
            ReferenceType = ReferenceType,
            From = From,
            Name = Name,
            Raw = Raw
        };
    }
}