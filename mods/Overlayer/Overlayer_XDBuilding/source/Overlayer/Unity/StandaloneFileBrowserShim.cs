using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SFB;

public class ExtensionFilter {
    public string Name { get; }
    public string[] Extensions { get; }

    public ExtensionFilter(string name, params string[] extensions) {
        Name = string.IsNullOrWhiteSpace(name) ? "Files" : name;
        Extensions = extensions ?? Array.Empty<string>();
    }
}

public static class StandaloneFileBrowser {
    private const int DialogTimeoutMilliseconds = 10 * 60 * 1000;
    private static readonly object DialogLock = new object();

    public static string[] OpenFilePanel(string title, string directory, ExtensionFilter[] extensions, bool multiselect) {
        string script = BuildOpenScript(
            title ?? string.Empty,
            GetSafeDirectory(directory),
            BuildOpenFilter(extensions),
            multiselect
        );

        return RunPowerShellDialog(script)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray();
    }

    public static string SaveFilePanel(string title, string directory, string defaultName, string extension) {
        string ext = NormalizeExtension(extension);
        string script = BuildSaveScript(
            title ?? string.Empty,
            GetSafeDirectory(directory),
            defaultName ?? string.Empty,
            ext,
            BuildSaveFilter(ext)
        );

        return RunPowerShellDialog(script).FirstOrDefault(path => !string.IsNullOrWhiteSpace(path)) ?? string.Empty;
    }

    private static string[] RunPowerShellDialog(string script) {
        lock(DialogLock) {
            try {
                string powershell = FindWindowsPowerShell();
                string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

                ProcessStartInfo startInfo = new ProcessStartInfo {
                    FileName = powershell,
                    Arguments = "-NoLogo -NoProfile -STA -ExecutionPolicy Bypass -EncodedCommand " + encoded,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using(Process process = Process.Start(startInfo)) {
                    if(process == null) return Array.Empty<string>();

                    Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
                    Task<string> stderrTask = process.StandardError.ReadToEndAsync();

                    if(!process.WaitForExit(DialogTimeoutMilliseconds)) {
                        try {
                            process.Kill();
                        } catch {
                        }
                        Trace.WriteLine("[SFB] PowerShell dialog timed out.");
                        return Array.Empty<string>();
                    }

                    string stdout = stdoutTask.GetAwaiter().GetResult();
                    string stderr = stderrTask.GetAwaiter().GetResult();

                    if(process.ExitCode != 0 && !string.IsNullOrWhiteSpace(stderr)) {
                        Trace.WriteLine("[SFB] PowerShell dialog error: " + stderr);
                    }

                    return stdout
                        .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                        .Select(line => line.Trim())
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .ToArray();
                }
            } catch(Exception ex) {
                Trace.WriteLine("[SFB] PowerShell dialog failed: " + ex);
                return Array.Empty<string>();
            }
        }
    }

    private static string FindWindowsPowerShell() {
        string systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string powershell = Path.Combine(systemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        return File.Exists(powershell) ? powershell : "powershell.exe";
    }

    private static string BuildOpenScript(string title, string directory, string filter, bool multiselect) {
        return
            "$ErrorActionPreference = 'Stop'\n" +
            "[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)\n" +
            "Add-Type -AssemblyName System.Windows.Forms\n" +
            "[System.Windows.Forms.Application]::EnableVisualStyles()\n" +
            "$dialog = New-Object System.Windows.Forms.OpenFileDialog\n" +
            "$dialog.Title = " + PsString(title) + "\n" +
            "$dialog.InitialDirectory = " + PsString(directory) + "\n" +
            "$dialog.Filter = " + PsString(filter) + "\n" +
            "$dialog.Multiselect = $" + (multiselect ? "true" : "false") + "\n" +
            "$dialog.CheckFileExists = $true\n" +
            "$dialog.CheckPathExists = $true\n" +
            "$dialog.RestoreDirectory = $true\n" +
            "$result = $dialog.ShowDialog()\n" +
            "if($result -eq [System.Windows.Forms.DialogResult]::OK) {\n" +
            "  foreach($file in $dialog.FileNames) { [Console]::WriteLine($file) }\n" +
            "}\n";
    }

    private static string BuildSaveScript(string title, string directory, string defaultName, string extension, string filter) {
        return
            "$ErrorActionPreference = 'Stop'\n" +
            "[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)\n" +
            "Add-Type -AssemblyName System.Windows.Forms\n" +
            "[System.Windows.Forms.Application]::EnableVisualStyles()\n" +
            "$dialog = New-Object System.Windows.Forms.SaveFileDialog\n" +
            "$dialog.Title = " + PsString(title) + "\n" +
            "$dialog.InitialDirectory = " + PsString(directory) + "\n" +
            "$dialog.FileName = " + PsString(defaultName) + "\n" +
            "$dialog.Filter = " + PsString(filter) + "\n" +
            "$dialog.DefaultExt = " + PsString(extension) + "\n" +
            "$dialog.AddExtension = $true\n" +
            "$dialog.OverwritePrompt = $true\n" +
            "$dialog.CheckPathExists = $true\n" +
            "$dialog.RestoreDirectory = $true\n" +
            "$result = $dialog.ShowDialog()\n" +
            "if($result -eq [System.Windows.Forms.DialogResult]::OK) { [Console]::WriteLine($dialog.FileName) }\n";
    }

    private static string PsString(string value) {
        return "'" + (value ?? string.Empty).Replace("'", "''") + "'";
    }

    private static string GetSafeDirectory(string directory) {
        if(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)) {
            return Path.GetFullPath(directory);
        }

        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Directory.Exists(docs) ? docs : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    }

    private static string BuildOpenFilter(IEnumerable<ExtensionFilter> filters) {
        List<string> parts = new List<string>();

        if(filters != null) {
            foreach(ExtensionFilter filter in filters.Where(f => f != null)) {
                string pattern = BuildPattern(filter.Extensions);
                if(!string.IsNullOrWhiteSpace(pattern)) {
                    parts.Add($"{filter.Name} ({pattern})");
                    parts.Add(pattern);
                }
            }
        }

        if(parts.Count == 0 || !parts.Any(part => part == "*.*")) {
            parts.Add("All files (*.*)");
            parts.Add("*.*");
        }

        return string.Join("|", parts);
    }

    private static string BuildSaveFilter(string extension) {
        string ext = NormalizeExtension(extension);
        if(string.IsNullOrWhiteSpace(ext) || ext == "*") {
            return "All files (*.*)|*.*";
        }

        return $"{ext.ToUpperInvariant()} files (*.{ext})|*.{ext}|All files (*.*)|*.*";
    }

    private static string BuildPattern(IEnumerable<string> extensions) {
        if(extensions == null) return "*.*";

        string[] patterns = extensions
            .Where(ext => !string.IsNullOrWhiteSpace(ext))
            .SelectMany(ext => ext.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
            .Select(NormalizeExtension)
            .Where(ext => !string.IsNullOrWhiteSpace(ext))
            .Select(ext => ext == "*" || ext == "*.*" ? "*.*" : "*." + ext)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return patterns.Length == 0 ? "*.*" : string.Join(";", patterns);
    }

    private static string NormalizeExtension(string extension) {
        if(string.IsNullOrWhiteSpace(extension)) return string.Empty;

        string ext = extension.Trim().TrimStart('.');
        return ext == "*.*" ? "*" : ext;
    }
}
