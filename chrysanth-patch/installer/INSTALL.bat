@echo off
setlocal EnableExtensions
title Cheque Writer v27.3 update
if not defined CW_APPDIR set "CW_APPDIR=C:\Program Files (x86)\Chrysanth\Cheque Writer"
set "SRC=%~dp0CSCheckWriter.exe"
set "TARGET=%CW_APPDIR%\CSCheckWriter.exe"
set "ORIG=%CW_APPDIR%\CSCheckWriter_v27_original.exe"

if /i "%~1"=="elevated" goto :elevated

echo ============================================================
echo   Cheque Writer - install v27.3 update  (step 1 of 2)
echo ============================================================
echo.

tasklist /fi "imagename eq CSCheckWriter.exe" 2>nul | find /i "CSCheckWriter.exe" >nul
if not errorlevel 1 (
  echo [STOP] Cheque Writer is open. Close it, then run INSTALL.bat again.
  goto :end_fail
)
if not exist "%SRC%" (
  echo [STOP] CSCheckWriter.exe is not next to INSTALL.bat.
  echo        Extract the whole ZIP to a folder first, then run INSTALL.bat from there.
  goto :end_fail
)
if not exist "%TARGET%" goto :notfound

set "VER="
for /f "usebackq delims=" %%v in (`powershell -NoProfile -Command "(Get-Item -LiteralPath $env:TARGET).VersionInfo.FileVersion"`) do set "VER=%%v"
echo Installed version: %VER%
echo %VER% | findstr /b "27." >nul
if errorlevel 1 (
  echo [STOP] This computer does not have version 27 installed.
  echo        This update is only for version 27. Nothing was changed.
  goto :end_fail
)

rem ---- back up the data file (done as the normal user, so the right profile is used)
set "DB=%APPDATA%\Chrysanth\Data\DB.CSDB"
for /f "usebackq delims=" %%t in (`powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmmss"`) do set "STAMP=%%t"
set "BKDIR=%USERPROFILE%\Desktop\ChequeWriter_Backup_%STAMP%"
mkdir "%BKDIR%" >nul 2>&1
if exist "%DB%" (
  copy /y "%DB%" "%BKDIR%\DB.CSDB" >nul || (echo [STOP] Could not back up the data file. & goto :end_fail)
  echo Data backed up to: "%BKDIR%\DB.CSDB"
) else (
  echo No data file found yet - nothing to back up.
)
copy /y "%TARGET%" "%BKDIR%\CSCheckWriter_before_update.exe" >nul
echo Program backed up to: %BKDIR%\CSCheckWriter_before_update.exe
echo.
echo Step 2: Windows will now ask for permission. Click YES.

if defined CW_NOELEVATE goto :elevated
net session >nul 2>&1
if not errorlevel 1 goto :elevated
powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -ArgumentList 'elevated' -Verb RunAs -Wait"
echo.
echo Finished. Read the result in the other window.
goto :end_ok

:elevated
echo ============================================================
echo   Cheque Writer - install v27.3 update  (step 2 of 2)
echo ============================================================
if not exist "%ORIG%" (
  copy /y "%TARGET%" "%ORIG%" >nul || (echo [FAILED] Could not keep the original program. & goto :end_fail)
  echo Original program kept as: CSCheckWriter_v27_original.exe
) else (
  echo Original program already kept earlier - not overwritten.
)
copy /y "%SRC%" "%TARGET%" >nul || (echo [FAILED] Could not copy the new program. & goto :end_fail)
fc /b "%SRC%" "%TARGET%" >nul || (echo [FAILED] Copy check failed. Run UNDO.bat. & goto :end_fail)
echo.
echo [DONE] Cheque Writer v27.3 installed.
echo        Open Cheque Writer, check Options - General: Test print must be UNTICKED for real cheques.
goto :end_ok

:notfound
echo [STOP] Cheque Writer was not found in:
echo        "%CW_APPDIR%"
goto :end_fail

:end_fail
echo.
if not defined CW_NOPAUSE pause
exit /b 1
:end_ok
echo.
if not defined CW_NOPAUSE pause
exit /b 0
