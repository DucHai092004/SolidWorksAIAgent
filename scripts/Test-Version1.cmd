@echo off
setlocal EnableExtensions
set ROOT=%~dp0..
set MSBUILD=C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe
set DRAWING_PROJECT=%ROOT%\src\SwMateAI.DrawingUnderstanding.IntegrationRunner\SwMateAI.DrawingUnderstanding.IntegrationRunner.csproj
set DRAWING_EXE=%ROOT%\src\SwMateAI.DrawingUnderstanding.IntegrationRunner\bin\x64\Debug\net48\SwMateAI.DrawingUnderstanding.IntegrationRunner.exe
set BOM_PROJECT=%ROOT%\src\SwMateAI.BomHierarchy.IntegrationRunner\SwMateAI.BomHierarchy.IntegrationRunner.csproj
set BOM_EXE=%ROOT%\src\SwMateAI.BomHierarchy.IntegrationRunner\bin\x64\Debug\net48\SwMateAI.BomHierarchy.IntegrationRunner.exe

echo ============================================================
echo SW-MATE AI VERSION 1 ACCEPTANCE TEST
echo ============================================================

echo.
echo [1/5] Build + Core automated tests
call "%ROOT%\scripts\Test-All.cmd"
if errorlevel 1 goto :fail

echo.
echo [2/5] Drawing Understanding integration
"%MSBUILD%" "%DRAWING_PROJECT%" /t:Rebuild /p:Configuration=Debug /p:Platform=x64 /v:minimal
if errorlevel 1 goto :fail
"%DRAWING_EXE%"
if errorlevel 1 goto :fail

echo.
echo [3/5] BOM hierarchy integration
"%MSBUILD%" "%BOM_PROJECT%" /t:Rebuild /p:Configuration=Debug /p:Platform=x64 /v:minimal
if errorlevel 1 goto :fail
"%BOM_EXE%"
if errorlevel 1 goto :fail

echo.
echo [4/5] Full SOLIDWORKS 2021 integration
call "%ROOT%\scripts\Test-SolidWorks.cmd"
if errorlevel 1 goto :fail

echo.
echo [5/5] Optional external OCR/render dependencies
where tesseract >nul 2>nul
if errorlevel 1 (echo [OPTIONAL] Tesseract not found - Issue #8 remains open.) else (echo [OK] Tesseract executable found.)
where pdftoppm >nul 2>nul
if errorlevel 1 (echo [OPTIONAL] pdftoppm not found - Issue #8 remains open.) else (echo [OK] pdftoppm executable found.)

echo.
echo ============================================================
echo VERSION 1 ACCEPTANCE: PASS
echo Required code and SOLIDWORKS regressions passed.
echo External OCR/render runtime validation is tracked in Issue #8.
echo ============================================================
exit /b 0

:fail
echo.
echo ============================================================
echo VERSION 1 ACCEPTANCE: FAIL
echo Check the first failing stage above.
echo ============================================================
exit /b 1
