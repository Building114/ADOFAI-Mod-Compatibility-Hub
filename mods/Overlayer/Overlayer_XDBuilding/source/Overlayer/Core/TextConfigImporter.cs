using Newtonsoft.Json.Linq;
using Overlayer.Models;
using Overlayer.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Overlayer.Core;

public static class TextConfigImporter {
    public static TextConfig Import(JToken node) {
        var config = ModelUtils.Unbox<TextConfig>(node);
        ImportRef(config, node);
        return config;
    }

    private static byte[] DecompressFont(byte[] raw) {
        const long maxSize = 64L * 1024 * 1024;
        using MemoryStream input = new(raw);
        using DeflateStream compressed = new(input, CompressionMode.Decompress);
        using MemoryStream output = new();
        byte[] buffer = new byte[81920];
        int count;
        while((count = compressed.Read(buffer, 0, buffer.Length)) > 0) {
            if(output.Length + count > maxSize) {
                throw new InvalidDataException("Embedded font is too large");
            }
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }

    public static void ImportRef(TextConfig config, JToken node) {
        if(config == null || node is not JObject obj || obj["References"] is not JArray refs) {
            return;
        }

        string fontsDir = Path.Combine(Main.Mod.Path, "References", "Fonts");
        foreach(var token in refs) {
            try {
                var reference = ModelUtils.Unbox<Reference>(token);
                if(reference == null || reference.ReferenceType != Reference.Type.Font ||
                   reference.Raw == null || string.IsNullOrWhiteSpace(reference.Name) ||
                   reference.Name != Path.GetFileName(reference.Name) ||
                   reference.Name == "." || reference.Name == ".." ||
                   reference.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) {
                    continue;
                }

                bool uniqueName = refs.Count(r => r is JObject candidate &&
                    string.Equals(candidate["ReferenceType"]?.ToString(), "Font", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(candidate["Name"]?.ToString(), reference.Name, StringComparison.OrdinalIgnoreCase)) == 1;
                bool mainFont = Reference.MatchesPath(config.Font, reference, uniqueName);
                bool fallbackFont = config.FallbackFonts?.Any(f => Reference.MatchesPath(f, reference, uniqueName)) == true;
                if(!mainFont && !fallbackFont) {
                    continue;
                }

                byte[] data = DecompressFont(reference.Raw);
                if(data.Length == 0) {
                    continue;
                }

                string targetPath = Reference.StoreEmbedded(fontsDir, reference.Name, data);
                string relPath = "{ModDir}References/Fonts/" + Path.GetFileName(targetPath);
                if(mainFont) {
                    config.Font = relPath;
                }
                if(fallbackFont) {
                    for(int i = 0; i < config.FallbackFonts.Length; i++) {
                        if(Reference.MatchesPath(config.FallbackFonts[i], reference, uniqueName)) {
                            config.FallbackFonts[i] = relPath;
                        }
                    }
                }
            } catch(Exception e) {
                Main.Logger?.Log("[TextConfigImporter] Failed to import font reference: " + e.Message);
            }
        }
    }

    public static JArray GetReferences(TextConfig text) {
        var references = new List<Reference>();

        if(!string.IsNullOrWhiteSpace(text.Font) && text.Font != "Default") {
            references.Add(Reference.GetReference(text.Font, Reference.Type.Font));
        }

        if(text.EnableFallbackFonts) {
            foreach(var fallback in text.FallbackFonts ?? Array.Empty<string>()) {
                if(!string.IsNullOrWhiteSpace(fallback) && fallback != "Default") {
                    references.Add(Reference.GetReference(fallback, Reference.Type.Font));
                }
            }
        }

        return ModelUtils.WrapList(references
            .Where(r => r != null)
            .Distinct()
            .ToList());
    }

    public static JArray GetJustReferences(TextConfig text) {
        var references = new List<Reference>();

        if(!string.IsNullOrWhiteSpace(text.Font) && text.Font != "Default") {
            references.Add(new Reference {
                Name = text.Font,
                ReferenceType = Reference.Type.Font
            });
        }

        if(text.EnableFallbackFonts) {
            foreach(var fallback in text.FallbackFonts ?? Array.Empty<string>()) {
                if(!string.IsNullOrWhiteSpace(fallback) && fallback != "Default") {
                    references.Add(new Reference {
                        Name = fallback,
                        ReferenceType = Reference.Type.Font
                    });
                }
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