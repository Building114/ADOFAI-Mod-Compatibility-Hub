using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
    private const int NativeFileBufferChars = 65536;
    private const int OFN_OVERWRITEPROMPT = 0x00000002;
    private const int OFN_HIDEREADONLY = 0x00000004;
    private const int OFN_NOCHANGEDIR = 0x00000008;
    private const int OFN_ALLOWMULTISELECT = 0x00000200;
    private const int OFN_PATHMUSTEXIST = 0x00000800;
    private const int OFN_FILEMUSTEXIST = 0x00001000;
    private const int OFN_EXPLORER = 0x00080000;
    private const int OFN_ENABLESIZING = 0x00800000;
    private static readonly object DialogLock = new object();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OPENFILENAME {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpstrFilter;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public IntPtr lpstrFileTitle;
        public int nMaxFileTitle;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpstrInitialDir;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOpenFileName(ref OPENFILENAME ofn);

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSaveFileName(ref OPENFILENAME ofn);

    [DllImport("comdlg32.dll")]
    private static extern int CommDlgExtendedError();

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    public static string[] OpenFilePanel(string title, string directory, ExtensionFilter[] extensions, bool multiselect) {
        bool handled;
        string[] nativeResult = RunNativeOpenDialog(
            title ?? string.Empty,
            GetSafeDirectory(directory),
            BuildNativeFilter(BuildOpenFilter(extensions)),
            multiselect,
            out handled
        );
        if(handled) {
            return nativeResult;
        }

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
        bool handled;
        string nativeResult = RunNativeSaveDialog(
            title ?? string.Empty,
            GetSafeDirectory(directory),
            defaultName ?? string.Empty,
            ext,
            BuildNativeFilter(BuildSaveFilter(ext)),
            out handled
        );
        if(handled) {
            return nativeResult;
        }

        string script = BuildSaveScript(
            title ?? string.Empty,
            GetSafeDirectory(directory),
            defaultName ?? string.Empty,
            ext,
            BuildSaveFilter(ext)
        );

        return RunPowerShellDialog(script).FirstOrDefault(path => !string.IsNullOrWhiteSpace(path)) ?? string.Empty;
    }

    private static string[] RunNativeOpenDialog(string title, string directory, string filter, bool multiselect, out bool handled) {
        handled = false;
        if(Environment.OSVersion.Platform != PlatformID.Win32NT) {
            return Array.Empty<string>();
        }

        lock(DialogLock) {
            IntPtr fileBuffer = IntPtr.Zero;
            try {
                fileBuffer = AllocateFileBuffer();
                var ofn = CreateOpenFileName(title, directory, filter, fileBuffer);
                if(multiselect) {
                    ofn.Flags |= OFN_ALLOWMULTISELECT;
                }

                if(GetOpenFileName(ref ofn)) {
                    handled = true;
                    return ParseOpenFileBuffer(fileBuffer);
                }

                int error = CommDlgExtendedError();
                if(error == 0) {
                    handled = true;
                    return Array.Empty<string>();
                }

                Trace.WriteLine($"[SFB] Native OpenFileDialog failed with 0x{error:X8}; falling back to PowerShell.");
                return Array.Empty<string>();
            } catch(Exception ex) {
                Trace.WriteLine("[SFB] Native OpenFileDialog failed: " + ex);
                return Array.Empty<string>();
            } finally {
                if(fileBuffer != IntPtr.Zero) {
                    Marshal.FreeHGlobal(fileBuffer);
                }
            }
        }
    }

    private static string RunNativeSaveDialog(string title, string directory, string defaultName, string extension, string filter, out bool handled) {
        handled = false;
        if(Environment.OSVersion.Platform != PlatformID.Win32NT) {
            return string.Empty;
        }

        lock(DialogLock) {
            IntPtr fileBuffer = IntPtr.Zero;
            try {
                fileBuffer = AllocateFileBuffer(defaultName);
                var ofn = CreateOpenFileName(title, directory, filter, fileBuffer);
                ofn.Flags = OFN_EXPLORER |
                            OFN_ENABLESIZING |
                            OFN_HIDEREADONLY |
                            OFN_NOCHANGEDIR |
                            OFN_PATHMUSTEXIST |
                            OFN_OVERWRITEPROMPT;
                ofn.lpstrDefExt = extension;

                if(GetSaveFileName(ref ofn)) {
                    handled = true;
                    return ReadNullTerminatedString(fileBuffer);
                }

                int error = CommDlgExtendedError();
                if(error == 0) {
                    handled = true;
                    return string.Empty;
                }

                Trace.WriteLine($"[SFB] Native SaveFileDialog failed with 0x{error:X8}; falling back to PowerShell.");
                return string.Empty;
            } catch(Exception ex) {
                Trace.WriteLine("[SFB] Native SaveFileDialog failed: " + ex);
                return string.Empty;
            } finally {
                if(fileBuffer != IntPtr.Zero) {
                    Marshal.FreeHGlobal(fileBuffer);
                }
            }
        }
    }

    private static OPENFILENAME CreateOpenFileName(string title, string directory, string filter, IntPtr fileBuffer) {
        return new OPENFILENAME {
            lStructSize = Marshal.SizeOf(typeof(OPENFILENAME)),
            hwndOwner = GetForegroundWindow(),
            lpstrFilter = filter,
            nFilterIndex = 1,
            lpstrFile = fileBuffer,
            nMaxFile = NativeFileBufferChars,
            lpstrInitialDir = directory,
            lpstrTitle = title,
            Flags = OFN_EXPLORER |
                    OFN_ENABLESIZING |
                    OFN_HIDEREADONLY |
                    OFN_NOCHANGEDIR |
                    OFN_PATHMUSTEXIST |
                    OFN_FILEMUSTEXIST
        };
    }

    private static IntPtr AllocateFileBuffer(string initialValue = "") {
        IntPtr buffer = Marshal.AllocHGlobal(NativeFileBufferChars * sizeof(char));
        char[] empty = new char[NativeFileBufferChars];
        Marshal.Copy(empty, 0, buffer, empty.Length);

        if(!string.IsNullOrEmpty(initialValue)) {
            char[] initial = (initialValue + "\0").Take(NativeFileBufferChars).ToArray();
            Marshal.Copy(initial, 0, buffer, initial.Length);
        }

        return buffer;
    }

    private static string[] ParseOpenFileBuffer(IntPtr buffer) {
        char[] chars = new char[NativeFileBufferChars];
        Marshal.Copy(buffer, chars, 0, chars.Length);

        List<string> parts = new List<string>();
        int start = 0;
        for(int i = 0; i < chars.Length; i++) {
            if(chars[i] != '\0') {
                continue;
            }

            if(i == start) {
                break;
            }

            parts.Add(new string(chars, start, i - start));
            start = i + 1;
        }

        if(parts.Count <= 1) {
            return parts.ToArray();
        }

        string directory = parts[0];
        return parts
            .Skip(1)
            .Select(file => Path.IsPathRooted(file) ? file : Path.Combine(directory, file))
            .ToArray();
    }

    private static string ReadNullTerminatedString(IntPtr buffer) {
        return Marshal.PtrToStringUni(buffer) ?? string.Empty;
    }

    private static string BuildNativeFilter(string filter) {
        return (filter ?? "All files (*.*)|*.*").Replace('|', '\0') + "\0\0";
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
