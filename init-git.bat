@echo off
REM DebugOpsMCP Git Initialization Script for Windows
REM This script initializes the git repository and creates the initial commit

setlocal enabledelayedexpansion

echo 🚀 Initializing DebugOpsMCP Git Repository...

REM Navigate to the project root
cd /d "%~dp0"
set PROJECT_ROOT=%cd%

echo 📁 Project root: %PROJECT_ROOT%

REM Check if git is available
git --version >nul 2>&1
if errorlevel 1 (
    echo ❌ Git is not installed or not available in PATH
    echo Please install Git from https://git-scm.com/
    pause
    exit /b 1
)

REM Initialize git repository if not already initialized
if not exist ".git" (
    echo 🔧 Initializing git repository...
    git init
    
    REM Set up initial configuration
    echo ⚙️ Setting up git configuration...
    git config --local core.autocrlf true
    git config --local core.ignorecase false
    git config --local pull.rebase false
) else (
    echo ✅ Git repository already initialized
)

REM Add all files to staging
echo 📦 Adding files to git staging area...
git add .

REM Check if there are any changes to commit
git diff --cached --quiet
if not errorlevel 1 (
    echo ℹ️ No changes to commit
    pause
    exit /b 0
)

REM Create the initial commit
echo 💾 Creating initial commit...
git commit -m "feat: initial DebugOpsMCP project setup

🏗️ Project Structure:
- Complete .NET 8 core server with MCP host implementation  
- VS Code extension with TypeScript for MCP client management
- Comprehensive test suite with unit and integration tests
- Documentation with ADRs, examples, and contribution guidelines

🔧 Core Features:
- Debug lifecycle tools (attach, launch, disconnect, terminate)
- Execution control (continue, pause, step over/into/out) 
- Breakpoint management (set, remove, list)
- Runtime inspection (stack trace, variables, evaluation)
- Thread management and debug status reporting
- Structured error handling and comprehensive logging

📚 Documentation:
- Architecture diagrams and design decisions
- Detailed debugging scenarios and examples
- API reference and troubleshooting guides
- Development setup and contribution guidelines

🚀 CI/CD:
- Architectural boundary enforcement
- Security scanning and vulnerability detection
- Multi-platform testing for .NET and Node.js
- Automated packaging and artifact generation

This establishes the foundation for Phase 1 MVP with all core 
debugging operations accessible through MCP for GitHub Copilot 
Agent Mode integration."

if errorlevel 1 (
    echo ❌ Failed to create initial commit
    pause
    exit /b 1
)

REM Display commit information
echo.
echo ✅ Initial commit created successfully!
echo.
git log --oneline -1
echo.

REM Display repository status
echo 📊 Repository Status:
for /f %%i in ('git status --porcelain ^| find /c /v ""') do echo    Files tracked: %%i
for /f "tokens=*" %%i in ('git branch --show-current') do echo    Branch: %%i
for /f "tokens=*" %%i in ('git log -1 --format^="%%h - %%s"') do echo    Latest commit: %%i

REM Show next steps
echo.
echo 🎯 Next Steps:
echo    1. Set up remote repository: git remote add origin ^<repository-url^>
echo    2. Push to remote: git push -u origin main
echo    3. Set up branch protection rules
echo    4. Configure CI/CD workflows
echo.
echo 🔧 Development:
echo    1. Build: dotnet build core/DebugOpsMCP.sln
echo    2. Test: dotnet test core/
echo    3. Run: dotnet run --project core/src/DebugOpsMCP.Host
echo.

echo 🎉 DebugOpsMCP repository is ready for development!
pause