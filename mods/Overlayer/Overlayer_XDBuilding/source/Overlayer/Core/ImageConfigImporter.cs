using Newtonsoft.Json.Linq;
using Overlayer.Models;
using Overlayer.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Overlayer.Core;

public static class ImageConfigImporter {
    public static ImageConfig Import(JToken node) {
        var config = ModelUtils.Unbox<ImageConfig>(node);
        ImportRef(config, node);
        return config;
    }

    private static byte[] DecompressImage(byte[] raw) {
        const long maxSize = 64L * 1024 * 1024;
        using MemoryStream input = new(raw);
        using DeflateStream compressed = new(input, CompressionMode.Decompress);
        using MemoryStream output = new();
        byte[] buffer = new byte[81920];
        int count;
        while((count = compressed.Read(buffer, 0, buffer.Length)) > 0) {
            if(output.Length + count > maxSize) {
                throw new InvalidDataException("Embedded image is too large");
            }
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }

    public static void ImportRef(ImageConfig config, JToken node) {
        if(config?.Images == null || node is not JObject obj || obj["References"] is not JArray refs) {
            return;
        }

        string imagesDir = Path.Combine(Main.Mod.Path, "References", "Images");
        foreach(var token in refs) {
            try {
                var reference = ModelUtils.Unbox<Reference>(token);
                if(reference == null || reference.ReferenceType != Reference.Type.Image ||
                   reference.Raw == null || string.IsNullOrWhiteSpace(reference.Name) ||
                   reference.Name != Path.GetFileName(reference.Name) ||
                   reference.Name == "." || reference.Name == ".." ||
                   reference.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) {
                    continue;
                }
                bool uniqueName = refs.Count(r => r is JObject candidate &&
                    string.Equals(candidate["ReferenceType"]?.ToString(), "Image", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(candidate["Name"]?.ToString(), reference.Name, StringComparison.OrdinalIgnoreCase)) == 1;
                if(!config.Images.Any(path => Reference.MatchesPath(path, reference, uniqueName))) {
                    continue;
                }

                byte[] data = DecompressImage(reference.Raw);
                if(data.Length == 0) {
                    continue;
                }

                string targetPath = Reference.StoreEmbedded(imagesDir, reference.Name, data);
                string relPath = "{ModDir}References/Images/" + Path.GetFileName(targetPath);
                for(int i = 0; i < config.Images.Count; i++) {
                    if(Reference.MatchesPath(config.Images[i], reference, uniqueName)) {
                        config.Images[i] = relPath;
                    }
                }
            } catch(Exception e) {
                Main.Logger?.Log("[ImageConfigImporter] Failed to import image reference: " + e.Message);
            }
        }
    }

    public static JArray GetReferences(ImageConfig config) {
        var references = new List<Reference>();
        foreach(var image in config.Images) {
            if(!string.IsNullOrWhiteSpace(image)) {
                references.Add(Reference.GetReference(image, Reference.Type.Image));
            }
        }
        return ModelUtils.WrapList(references.Where(r => r != null).Distinct().ToList());
    }

    public static JArray GetJustReferences(ImageConfig config) {
        var references = new List<Reference>();

        foreach(var image in config.Images) {
            if(!string.IsNullOrWhiteSpace(image)) {
                references.Add(new Reference {
                    Name = image,
                    ReferenceType = Reference.Type.Image
                });
            }
        }

        var set = new HashSet<string>();
        var result = new List<Reference>();

        foreach(var r in references) {
            if(r == null) {
                continue;
            }

            string key = $"{r.ReferenceType}:{r.Name}";
            if(set.Add(key)) {
                result.Add(r);
            }
        }

        return ModelUtils.WrapList(result);
    }
}
