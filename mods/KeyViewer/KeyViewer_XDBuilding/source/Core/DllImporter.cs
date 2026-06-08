﻿using KeyViewer;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Overlayer.Core;

public class DllImporter {
    private const string NCalcAssemblyName = "NCalc";
    private const string NCalcResourceName = "KeyViewer.Dependencies.NCalc.dll";
    private static bool nCalcInitialized = false;

    public static bool NCalcInitialize() {
        if(nCalcInitialized || IsNCalcLoaded()) {
            nCalcInitialized = true;
            return true;
        }

        string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? Main.Mod.Path;

        string[] candidates = {
            Path.Combine(Main.Mod.Path, "lib", "NCalc.dll"),
            Path.Combine(Main.Mod.Path, "NCalc.dll"),
            Path.Combine(assemblyDirectory, "lib", "NCalc.dll"),
            Path.Combine(assemblyDirectory, "NCalc.dll")
        };

        foreach(string dll in candidates.Distinct(StringComparer.OrdinalIgnoreCase)) {
            if(!File.Exists(dll)) continue;

            try {
                Assembly.LoadFrom(dll);
                if(IsNCalcLoaded()) {
                    nCalcInitialized = true;
                    Main.Logger.Log($"Loaded NCalc: {dll}");
                    return true;
                }
            } catch(Exception ex) {
                Main.Logger.Error($"Failed to load NCalc from {dll}: {ex}");
            }
        }

        try {
            Assembly owner = Assembly.GetExecutingAssembly();
            using(Stream stream = owner.GetManifestResourceStream(NCalcResourceName)) {
                if(stream != null) {
                    byte[] raw = new byte[checked((int)stream.Length)];
                    int offset = 0;
                    while(offset < raw.Length) {
                        int read = stream.Read(raw, offset, raw.Length - offset);
                        if(read <= 0) break;
                        offset += read;
                    }

                    if(offset == raw.Length) {
                        Assembly.Load(raw);
                        if(IsNCalcLoaded()) {
                            nCalcInitialized = true;
                            Main.Logger.Log("Loaded embedded NCalc dependency.");
                            return true;
                        }
                    }
                }
            }
        } catch(Exception ex) {
            Main.Logger.Error($"Failed to load embedded NCalc: {ex}");
        }

        Main.Logger.Error(
            $"NCalc dependency is unavailable. Expected external file at " +
            $"{Path.Combine(Main.Mod.Path, "lib", "NCalc.dll")} or embedded resource {NCalcResourceName}."
        );
        return false;
    }

    private static bool IsNCalcLoaded() =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Any(a => a.GetName().Name.Equals(NCalcAssemblyName, StringComparison.OrdinalIgnoreCase));
}
