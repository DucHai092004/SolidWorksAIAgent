@echo off
setlocal
set ROOT=%~dp0..
set MSBUILD=C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe
set PROJECT=%ROOT%\src\SwMateAI.SolidWorks.IntegrationRunner\SwMateAI.SolidWorks.IntegrationRunner.csproj
set EXE=%ROOT%\src\SwMateAI.SolidWorks.IntegrationRunner\bin\x64\Debug\net48\SwMateAI.SolidWorks.IntegrationRunner.exe

echo === SW-MATE AI SOLIDWORKS 2021 integration verification ===
"%MSBUILD%" "%PROJECT%" /t:Rebuild /p:Configuration=Debug /p:Platform=x64 /v:minimal
if errorlevel 1 exit /b %errorlevel%

"%EXE%"
exit /b %errorlevel%
