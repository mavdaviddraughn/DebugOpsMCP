@echo off
REM DebugOpsMCP Git Status Check
echo Checking DebugOpsMCP Git Repository Status...
echo.

REM Navigate to project root
cd /d "%~dp0"

echo Repository Information:
echo =====================
git log --oneline -1
echo.
git status --short
echo.

echo Branch Information:
echo ==================
git branch -v
echo.

echo Recent Activity:
echo ===============
git log --oneline -5
echo.

echo File Count:
echo ==========
for /f %%i in ('git ls-files ^| find /c /v ""') do echo    Total tracked files: %%i
echo.

echo Repository is ready for development!
echo.
echo Quick Commands:
echo - Build: dotnet build core/DebugOpsMCP.sln
echo - Test: dotnet test core/
echo - Run: dotnet run --project core/src/DebugOpsMCP.Host
echo.

pause