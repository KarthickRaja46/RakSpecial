@echo off
rem Builds one RAKSpecial.exe that runs on any 64-bit Windows 10/11 PC without installing .NET,
rem then makes the setup folder + ZIP for other computers.
rem Output: publish\RAKSpecial.exe  and  Share\RAK Special - Setup.zip
setlocal
cd /d "%~dp0"
dotnet test || (echo Tests failed - not publishing. & exit /b 1)
dotnet publish src\ChequeWriter\ChequeWriter.csproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=none -o publish || exit /b 1
if not exist "Share\RAK Special - Setup" mkdir "Share\RAK Special - Setup"
copy /y publish\RAKSpecial.exe "Share\RAK Special - Setup\" >nul || exit /b 1
copy /y setup\*.* "Share\RAK Special - Setup\" >nul || exit /b 1
powershell -NoProfile -Command "Compress-Archive -Path 'Share\RAK Special - Setup' -DestinationPath 'Share\RAK Special - Setup.zip' -Force" || exit /b 1
echo.
echo Done: %~dp0publish\RAKSpecial.exe
echo Share: %~dp0Share\RAK Special - Setup.zip
