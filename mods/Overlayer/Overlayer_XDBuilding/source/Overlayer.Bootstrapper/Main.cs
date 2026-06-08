using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using static UnityModManagerNet.UnityModManager;

namespace Overlayer.Bootstrapper;

public static class Main {
    private static readonly string[] dependencyDlls = [
        // Low-level dependencies first.
        "System.Runtime.CompilerServices.Unsafe",
        "System.Numerics.Vectors",
        "System.Buffers",
        "System.Memory",
        "System.Threading.Tasks.Extensions",
        "System.ValueTuple",
        "System.CodeDom",
        "System.Management",
        "System.Security.AccessControl",
        "System.Security.Principal.Windows",
        "System.Threading.AccessControl",
        "Microsoft.Win32.Registry",
        "System.Security.Permissions",
        "System.Configuration.ConfigurationManager",
        "System.Diagnostics.EventLog",

        // Main third-party libraries.
        "NCalc",
        "Acornima",
        "Esprima",
        "Jint",
        "HidSharp",
        "DiskInfoToolkit",
        "RAMSPDToolkit-NDD",
        "BlackSharp.Core",
        "LibreHardwareMonitorLib",
        "Vostok.Sys.Metrics.PerfCounters"
    ];

    private static readonly string[] requiredDlls = [
        "System.Memory",
        "System.Runtime.CompilerServices.Unsafe",
        "System.Numerics.Vectors",
        "Jint",
        "Acornima",
        "NCalc",
        "Vostok.Sys.Metrics.PerfCounters",
        "LibreHardwareMonitorLib"
    ];

    // These assemblies are shared .NET base libraries. Loading Overlayer/lib copies first can
    // pollute ADOFAI's global runtime and break editor saving through System.Text.Json.
    // Prefer an already-loaded copy, then the game's Managed folder.
    private static readonly HashSet<string> preferGameManagedDlls = new(StringComparer.OrdinalIgnoreCase) {
        "System.Buffers",
        "System.Memory",
        "System.Runtime.CompilerServices.Unsafe",
        "System.Threading.Tasks.Extensions"
    };

    private static readonly string FailName = "Overlayer [FAIL]";

    public static void Load(ModEntry modEntry) {
        void SetFail() => modEntry.Info.DisplayName = FailName;

        string libPath = Path.Combine(modEntry.Path, "lib");
        if(!Directory.Exists(libPath)) {
            modEntry.Logger.Log("/lib/ folder does not exist.");
            SetFail();
            return;
        }

        string managedPath = FindGameManagedPath(modEntry.Path);
        if(string.IsNullOrEmpty(managedPath)) {
            modEntry.Logger.Log("[Bootstrapper] Game Managed folder not found. Some System.* dependency fixes may not work.");
        } else {
            modEntry.Logger.Log($"[Bootstrapper] Game Managed folder: {managedPath}");
        }

        var allDllFiles = Directory.GetFiles(libPath, "*.dll", SearchOption.AllDirectories);
        modEntry.Logger.Log($"Found {allDllFiles.Length} DLL(s) in /lib/");

        var dllByName = allDllFiles
            .GroupBy(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderBy(path => path.Length).First(), StringComparer.OrdinalIgnoreCase);

        foreach(string name in dependencyDlls) {
            if(!TryLoadDependency(name, dllByName, managedPath, modEntry, SetFail)) {
                return;
            }
        }

        foreach(var required in requiredDlls) {
            if(GetLoadedAssembly(required) == null) {
                modEntry.Logger.Log($"[ERROR] Required assembly '{required}' not loaded.");
                SetFail();
                return;
            }
        }

        string mainPath = Path.Combine(modEntry.Path, "Overlayer.dll");
        if(!File.Exists(mainPath)) {
            modEntry.Logger.Log("Overlayer.dll not found");
            SetFail();
            return;
        }

        try {
            var mainAss = Assembly.Load(File.ReadAllBytes(mainPath));
            modEntry.Logger.Log("Loaded Overlayer.dll successfully");
            typeof(ModEntry).GetField("mAssembly", (BindingFlags)15420).SetValue(modEntry, mainAss);
            mainAss.GetType("Overlayer.Main").GetMethod("Load").Invoke(null, [modEntry]);
            modEntry.Logger.Log("Overlayer.Main.Load invoked successfully");
        } catch(Exception e) {
            modEntry.Logger.Log($"Failed to load or invoke Overlayer.dll: {e}");
            SetFail();
        }
    }

    private static bool TryLoadDependency(
        string name,
        Dictionary<string, string> dllByName,
        string managedPath,
        ModEntry modEntry,
        Action setFail
    ) {
        var alreadyLoaded = GetLoadedAssembly(name);
        if(alreadyLoaded != null) {
            modEntry.Logger.Log($"Already loaded: {DescribeAssembly(alreadyLoaded)}");
            return true;
        }

        if(preferGameManagedDlls.Contains(name)) {
            if(!string.IsNullOrEmpty(managedPath)) {
                string managedDllPath = Path.Combine(managedPath, name + ".dll");
                if(File.Exists(managedDllPath)) {
                    if(TryLoadFromPath(name, managedDllPath, "game Managed", modEntry, setFail)) {
                        return true;
                    }
                }
            }

            if(dllByName.ContainsKey(name)) {
                modEntry.Logger.Log($"[Bootstrapper] Skipped Overlayer/lib copy of shared dependency: {name}");
            }
            return true;
        }

        if(!dllByName.TryGetValue(name, out string dllPath)) {
            return true;
        }

        return TryLoadFromPath(name, dllPath, "Overlayer/lib", modEntry, setFail);
    }

    private static bool TryLoadFromPath(
        string expectedName,
        string dllPath,
        string sourceName,
        ModEntry modEntry,
        Action setFail
    ) {
        try {
            var assembly = Assembly.Load(File.ReadAllBytes(dllPath));
            modEntry.Logger.Log($"Loaded from {sourceName}: {DescribeAssembly(assembly)}");

            if(!assembly.GetName().Name.Equals(expectedName, StringComparison.OrdinalIgnoreCase)) {
                modEntry.Logger.Log($"[Bootstrapper] Warning: expected {expectedName}, but loaded {assembly.GetName().Name} from {dllPath}");
            }

            return true;
        } catch(Exception e) {
            modEntry.Logger.Log($"Failed to load {expectedName} from {sourceName} ({dllPath}): {e}");
            setFail();
            return false;
        }
    }

    private static Assembly GetLoadedAssembly(string name) {
        return AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static string DescribeAssembly(Assembly assembly) {
        string location;
        try {
            location = string.IsNullOrEmpty(assembly.Location) ? "<in-memory>" : assembly.Location;
        } catch {
            location = "<unknown>";
        }

        return $"{assembly.GetName().Name}, Version={assembly.GetName().Version}, Location={location}";
    }

    private static string FindGameManagedPath(string modPath) {
        try {
            var current = new DirectoryInfo(modPath);
            for(int i = 0; i < 8 && current != null; i++) {
                string candidate = Path.Combine(current.FullName, "A Dance of Fire and Ice_Data", "Managed");
                if(Directory.Exists(candidate)) {
                    return candidate;
                }

                current = current.Parent;
            }
        } catch {
            // Ignore and return null below.
        }

        return null;
    }
}
