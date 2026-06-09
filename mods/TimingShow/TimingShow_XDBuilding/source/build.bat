@echo off
setlocal

where msbuild >nul 2>nul
if errorlevel 1 (
  echo ERROR: msbuild was not found in PATH.
  echo Run this from a Visual Studio Developer Command Prompt, or add MSBuild to PATH.
  exit /b 1
)

if "%~1"=="" (
  if "%ADOFAI_GAME_DIR%"=="" (
    echo ERROR: ADOFAI_GAME_DIR is not set.
    echo Example:
    echo set "ADOFAI_GAME_DIR=C:\Path\To\A Dance of Fire and Ice"
    exit /b 1
  )
  if "%UMM_INSTALLER_DIR%"=="" if "%UMM_RUNTIME_DIR%"=="" (
    echo ERROR: UMM_INSTALLER_DIR or UMM_RUNTIME_DIR must be set.
    echo Example:
    echo set "UMM_INSTALLER_DIR=C:\Path\To\UnityModManagerInstaller"
    echo set "UMM_RUNTIME_DIR=C:\Path\To\UnityModManagerRuntime"
    exit /b 1
  )
)

msbuild TimingShow.sln /p:Configuration=Release /p:Platform="Any CPU" /v:m %*
if errorlevel 1 exit /b 1

if not exist "TimingShow\bin\Release\TimingShow.dll" (
  echo ERROR: build finished, but TimingShow\bin\Release\TimingShow.dll was not found.
  exit /b 1
)
if not exist "TimingShow\bin\Release\Info.json" (
  echo ERROR: build finished, but TimingShow\bin\Release\Info.json was not found.
  exit /b 1
)

if not exist "..\release" mkdir "..\release"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -LiteralPath 'TimingShow\bin\Release\TimingShow.dll','TimingShow\bin\Release\Info.json' -DestinationPath '..\release\TimingShow.zip' -Force"
if errorlevel 1 exit /b 1

echo OK: ..\release\TimingShow.zip
endlocal
