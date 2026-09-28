@echo off
setlocal
set ROOT=%~dp0..
set MSBUILD=C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe

echo === SW-MATE AI automated verification ===
echo [1/2] Building full solution without COM registration...
"%MSBUILD%" "%ROOT%\src\SwMateAI.sln" /t:Rebuild /p:Configuration=Debug /p:Platform=x64 /p:XCadRegDll=false /v:minimal
if errorlevel 1 exit /b %errorlevel%

echo [2/2] Running Core automated tests...
dotnet test "%ROOT%\src\SwMateAI.Core.Tests\SwMateAI.Core.Tests.csproj" -c Debug --no-restore --logger "console;verbosity=normal"
if errorlevel 1 exit /b %errorlevel%

echo PASS: build + automated Core tests.
exit /b 0
