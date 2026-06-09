@echo off
rem Example only. Copy this file to run.local.bat and edit paths for your machine.

set "GAME_PATH=C:\Path\To\A Dance of Fire and Ice"
set "GAME_EXE=A Dance of Fire and Ice.exe"
set "TARGET=%GAME_PATH%\Mods\TimingShow"

rem Build dependencies. You may also set these globally instead of editing this example.
set "ADOFAI_GAME_DIR=%GAME_PATH%"
set "UMM_INSTALLER_DIR=C:\Path\To\UnityModManagerInstaller"
set "UMM_RUNTIME_DIR=C:\Path\To\UnityModManagerRuntime"

msbuild TimingShow.sln /p:Configuration=Debug /p:Platform="Any CPU" /v:m
if errorlevel 1 exit /b 1

if not exist "%TARGET%" mkdir "%TARGET%"
copy /y "TimingShow\bin\Debug\TimingShow.dll" "%TARGET%\"
copy /y "TimingShow\bin\Debug\Info.json" "%TARGET%\"

start "" "%GAME_PATH%\%GAME_EXE%"
pause
