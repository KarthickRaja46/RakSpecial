@echo off
setlocal EnableExtensions
title Cheque Writer - undo v27.3 update
if not defined CW_APPDIR set "CW_APPDIR=C:\Program Files (x86)\Chrysanth\Cheque Writer"
set "TARGET=%CW_APPDIR%\CSCheckWriter.exe"
set "ORIG=%CW_APPDIR%\CSCheckWriter_v27_original.exe"

if /i "%~1"=="elevated" goto :elevated

tasklist /fi "imagename eq CSCheckWriter.exe" 2>nul | find /i "CSCheckWriter.exe" >nul
if not errorlevel 1 (
  echo [STOP] Cheque Writer is open. Close it, then run UNDO.bat again.
  goto :end_fail
)
if not exist "%ORIG%" (
  echo [STOP] No original program found ^(CSCheckWriter_v27_original.exe^).
  echo        Nothing to undo.
  goto :end_fail
)
echo Windows will now ask for permission. Click YES.
if defined CW_NOELEVATE goto :elevated
net session >nul 2>&1
if not errorlevel 1 goto :elevated
powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -ArgumentList 'elevated' -Verb RunAs -Wait"
goto :end_ok

:elevated
copy /y "%ORIG%" "%TARGET%" >nul || (echo [FAILED] Could not restore the original program. & goto :end_fail)
fc /b "%ORIG%" "%TARGET%" >nul || (echo [FAILED] Restore check failed. & goto :end_fail)
echo [DONE] Original Cheque Writer program restored.
goto :end_ok

:end_fail
echo.
if not defined CW_NOPAUSE pause
exit /b 1
:end_ok
echo.
if not defined CW_NOPAUSE pause
exit /b 0
