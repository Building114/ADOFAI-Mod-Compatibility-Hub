# Third Party Notices

This project is based on Overlayer and keeps the upstream third-party notices.

## RapidGUI

RapidGUI is included in source form.

License:
- MIT License

## UnityCodeEditor

UnityCodeEditor-related code is included in source form.

Upstream notice:
- Unlicensed project using MIT-licensed code.

## Jint

Jint is referenced through NuGet as `Jint` 4.7.1.

License:
- BSD-2-Clause License

Notice:
- This repository does not claim ownership of Jint.
- This source package does not claim to bundle a modified `modlist-org/jint` DLL.
- If a future binary release includes a modified Jint DLL, that release should clearly state where the corresponding source or upstream fork can be found.

## JipperResourcePack reference

Earlier XDB-related notes mentioned JipperResourcePack as a public reference for changed ADOFAI field names.

No JipperResourcePack source code is bundled in this repository unless explicitly stated elsewhere.
If code is copied from that project in the future, its license must be checked and included here.

## NuGet dependencies

This project uses NuGet `PackageReference` entries listed in `Overlayer/Overlayer.csproj`.

Known dependencies include:

- Acornima 1.3.2
- BlackSharp.Core 1.0.7
- DiskInfoToolkit 1.1.2
- Esprima 3.0.5
- HidSharp 2.6.4
- Jint 4.7.1
- LibreHardwareMonitorLib 0.9.6
- ncalc 1.3.8
- RAMSPDToolkit-NDD 1.4.2
- System.Buffers 4.6.1
- System.CodeDom 10.0.2
- System.IO.Compression.ZipFile 4.3.0
- System.Management 10.0.2
- System.Memory 4.6.3
- System.Numerics.Vectors 4.6.1
- System.Runtime.CompilerServices.Unsafe 6.1.2
- System.Security.AccessControl 6.0.0
- System.Security.Principal.Windows 5.0.0
- System.Threading.AccessControl 10.0.3
- System.Threading.Tasks.Extensions 4.6.3
- Vostok.Sys.Metrics.PerfCounters 0.0.8

These packages are not committed into this repository.
They should be restored through NuGet when building the project.

For binary releases, included dependency DLLs should keep their original license notices where required.
