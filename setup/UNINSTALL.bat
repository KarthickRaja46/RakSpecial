@echo off
setlocal EnableExtensions
title RAK Special - uninstall
if not defined RS_DIR set "RS_DIR=%LOCALAPPDATA%\Programs\RAK Special"
if not defined RS_MENU set "RS_MENU=%APPDATA%\Microsoft\Windows\Start Menu\Programs"

tasklist /fi "imagename eq RAKSpecial.exe" 2>nul | find /i "RAKSpecial.exe" >nul
if not errorlevel 1 (
  echo [STOP] RAK Special is open. Close it, then run UNINSTALL.bat again.
  goto :end_fail
)
echo This removes the RAK Special program and its shortcuts.
echo Your cheque register, payees and layouts are KEPT in:
echo   "%APPDATA%\RAK Special"
choice /c YN /m "Remove RAK Special"
if errorlevel 2 (echo Nothing was changed. & goto :end_fail)

if exist "%RS_DIR%\RAKSpecial.exe" del /f /q "%RS_DIR%\RAKSpecial.exe"
rd "%RS_DIR%" >nul 2>&1
del /f /q "%RS_MENU%\RAK Special.lnk" >nul 2>&1
powershell -NoProfile -Command "$d = if ($env:RS_DESKTOP) { $env:RS_DESKTOP } else { [Environment]::GetFolderPath('Desktop') }; Remove-Item -LiteralPath (Join-Path $d 'RAK Special.lnk') -ErrorAction SilentlyContinue" >nul 2>&1
if exist "%RS_DIR%\RAKSpecial.exe" (echo [FAILED] Could not remove the program. & goto :end_fail)
echo [DONE] RAK Special removed. Your data is still in "%APPDATA%\RAK Special".
goto :end_ok

:end_fail
echo.
if not defined RS_NOPAUSE pause
exit /b 1
:end_ok
echo.
if not defined RS_NOPAUSE pause
exit /b 0
