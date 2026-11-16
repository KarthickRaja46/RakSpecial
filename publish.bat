@echo off
rem Builds RAK Special for release:
rem   publish\RAKSpecial.exe                    one exe, runs on 64-bit Windows 10/11 without installing .NET
rem   Share\RAKSpecial-Setup-<version>.exe      setup wizard for new users (needs Inno Setup 6)
rem   Share\RAKSpecial-Portable-<version>.zip   no-install copy, data kept beside the exe
setlocal
cd /d "%~dp0"
dotnet test || (echo Tests failed - not publishing. & exit /b 1)
dotnet publish src\ChequeWriter\ChequeWriter.csproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=none -o publish || exit /b 1

for /f %%v in ('powershell -NoProfile -Command "$v = (Get-Item publish\RAKSpecial.exe).VersionInfo; '{0}.{1}.{2}' -f $v.FileMajorPart, $v.FileMinorPart, $v.FileBuildPart"') do set "VER=%%v"
if not exist Share mkdir Share

rem ---- portable ZIP ----
set "PORT=%TEMP%\RAK Special %VER% portable"
if exist "%PORT%" rd /s /q "%PORT%"
mkdir "%PORT%"
copy /y publish\RAKSpecial.exe "%PORT%\" >nul || exit /b 1
copy /y "installer\READ ME FIRST.txt" "%PORT%\" >nul || exit /b 1
type nul > "%PORT%\portable.txt"
powershell -NoProfile -Command "Compress-Archive -Path '%PORT%\*' -DestinationPath 'Share\RAKSpecial-Portable-%VER%.zip' -Force" || exit /b 1
rd /s /q "%PORT%"

rem ---- setup wizard ----
set "ISCC="
for %%p in ("%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" "%ProgramFiles%\Inno Setup 6\ISCC.exe") do if exist %%p set "ISCC=%%~p"
if not defined ISCC for %%p in (ISCC.exe) do set "ISCC=%%~$PATH:p"
if not defined ISCC (
  echo.
  echo Inno Setup 6 was not found, so the setup wizard was not built.
  echo Install it once with:  winget install JRSoftware.InnoSetup
  goto :done
)
"%ISCC%" /Q installer\RAKSpecial.iss || exit /b 1

:done
echo.
echo Done (version %VER%):
echo   %~dp0publish\RAKSpecial.exe
if exist "Share\RAKSpecial-Setup-%VER%.exe" echo   %~dp0Share\RAKSpecial-Setup-%VER%.exe
echo   %~dp0Share\RAKSpecial-Portable-%VER%.zip
