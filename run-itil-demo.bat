@echo off
REM Quick-start script for ITIL v4 Mastery Pack Demo
REM Run from SmallMind root directory

echo ========================================================================
echo   ITIL v4 Mastery Pack - Quick Start Demo
echo ========================================================================
echo.

REM Check if we're in the right directory
if not exist "data\pretrained\itil_v4_mastery" (
    echo Error: Please run this script from the SmallMind root directory
    exit /b 1
)

echo [OK] Found ITIL v4 pack
echo.

REM Build the demo project
echo [BUILD] Building demo application...
cd examples\ItilPackDemo
dotnet build --configuration Release --no-restore

echo.
echo [RUN] Running demo...
echo.

REM Run the demo
dotnet run --configuration Release --no-build

echo.
echo ========================================================================
echo   Demo complete! Check the output above for results.
echo ========================================================================

cd ..\..
