@echo off
setlocal

rem Builds bin\AMUI.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
rem No SDK, Visual Studio or downloads needed.

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo Couldn't find the .NET Framework C# compiler at %WINDIR%\Microsoft.NET.
    exit /b 1
)

cd /d "%~dp0"
if not exist bin mkdir bin

"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /warnaserror+ ^
    /win32icon:assets\amui.ico ^
    /resource:assets\amui.ico,AMUI.amui.ico ^
    /resource:assets\amui.png,AMUI.amui.png ^
    /reference:System.dll /reference:System.Core.dll ^
    /reference:System.Drawing.dll /reference:System.Windows.Forms.dll ^
    /reference:System.Web.Extensions.dll ^
    /out:bin\AMUI.exe src\*.cs
if errorlevel 1 exit /b 1

echo Built bin\AMUI.exe
