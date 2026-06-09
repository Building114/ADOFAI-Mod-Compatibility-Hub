@echo off
rem Example only. Copy this file to run.local.bat and edit paths for your machine.

set "SOURCE=TimingShow\bin\Debug"
set "TARGET=C:\Path\To\A Dance of Fire and Ice\Mods\TimingShow"
set "GAME_PATH=C:\Path\To\A Dance of Fire and Ice"
set "GAME_EXE=A Dance of Fire and Ice.exe"

msbuild TimingShow.sln /p:Configuration=Debug /p:Platform="Any CPU" /t:Build

if not exist "%TARGET%" mkdir "%TARGET%"
copy /y "%SOURCE%\TimingShow.dll" "%TARGET%\"
copy /y "%SOURCE%\Info.json" "%TARGET%\"

start "" "%GAME_PATH%\%GAME_EXE%"
pause
