@echo off
setlocal EnableExtensions
title RAK Special - install
rem Installs for the current Windows user only - no administrator permission needed.
rem Running it again updates the program; saved data (%APPDATA%\RAK Special) is kept.
if not defined RS_DIR set "RS_DIR=%LOCALAPPDATA%\Programs\RAK Special"
if not defined RS_MENU set "RS_MENU=%APPDATA%\Microsoft\Windows\Start Menu\Programs"
set "SRC=%~dp0RAKSpecial.exe"
set "EXE=%RS_DIR%\RAKSpecial.exe"

echo ============================================================
echo   RAK Special - install
echo ============================================================
echo.
if not exist "%SRC%" (
  echo [STOP] RAKSpecial.exe is not next to INSTALL.bat.
  echo        Right-click the ZIP - Extract All - then run INSTALL.bat from the extracted folder.
  goto :end_fail
)
tasklist /fi "imagename eq RAKSpecial.exe" 2>nul | find /i "RAKSpecial.exe" >nul
if not errorlevel 1 (
  echo [STOP] RAK Special is open. Close it, then run INSTALL.bat again.
  goto :end_fail
)

if exist "%EXE%" (echo Updating RAK Special in: "%RS_DIR%") else (echo Installing RAK Special in: "%RS_DIR%")
if not exist "%RS_DIR%" mkdir "%RS_DIR%" >nul 2>&1
copy /y "%SRC%" "%EXE%" >nul || (echo [FAILED] Could not copy the program. & goto :end_fail)
fc /b "%SRC%" "%EXE%" >nul || (echo [FAILED] Copy check failed. Run INSTALL.bat again. & goto :end_fail)
echo Program copied.

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$w = New-Object -ComObject WScript.Shell;" ^
  "$desk = if ($env:RS_DESKTOP) { $env:RS_DESKTOP } else { [Environment]::GetFolderPath('Desktop') };" ^
  "foreach ($dir in $desk, $env:RS_MENU) { $s = $w.CreateShortcut((Join-Path $dir 'RAK Special.lnk')); $s.TargetPath = $env:EXE; $s.WorkingDirectory = $env:RS_DIR; $s.Description = 'RAK Special - RAK Bank cheque printing'; $s.Save() }" >nul 2>&1
if exist "%RS_MENU%\RAK Special.lnk" (
  echo Shortcut "RAK Special" added to the Desktop and the Start menu.
) else (
  echo Note: no shortcut was made. Start the program from: "%EXE%"
)
echo.
echo [DONE] RAK Special is installed.
echo        Open it, choose the cheque book (RAK Business or RAK Old),
echo        and do one "Test print on plain paper" before the first real cheque.
goto :end_ok

:end_fail
echo.
if not defined RS_NOPAUSE pause
exit /b 1
:end_ok
echo.
if not defined RS_NOPAUSE pause
exit /b 0
