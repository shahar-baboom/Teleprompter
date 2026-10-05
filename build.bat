@echo off
setlocal
cd /d "%~dp0"
rem Builds Teleprompter.exe using the C# compiler that ships inside Windows (.NET Framework 4.8).
rem Nothing needs to be installed.
set "FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
if not exist "%FW%\csc.exe" set "FW=%WINDIR%\Microsoft.NET\Framework\v4.0.30319"
if not exist "%FW%\csc.exe" (
  echo Could not find the .NET Framework 4 compiler in %WINDIR%\Microsoft.NET
  pause
  exit /b 1
)
set "WPF=%FW%\WPF"
echo Building Teleprompter.exe ...
"%FW%\csc.exe" /nologo /target:winexe /optimize+ /codepage:65001 /platform:anycpu ^
  /out:Teleprompter.exe /win32manifest:app.manifest /resource:src\Theme.xaml,Teleprompter.Theme.xaml ^
  /r:"%WPF%\PresentationFramework.dll" /r:"%WPF%\PresentationCore.dll" /r:"%WPF%\WindowsBase.dll" ^
  /r:"%FW%\System.Xaml.dll" /r:System.dll /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  src\*.cs
if errorlevel 1 (
  echo.
  echo BUILD FAILED - see the messages above.
  pause
  exit /b 1
)
echo.
echo Done: Teleprompter.exe is ready. You can copy it anywhere - it is a single standalone file.
start "" "%~dp0Teleprompter.exe"
