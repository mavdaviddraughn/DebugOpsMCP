#!/bin/bash

# DebugOpsMCP Git Initialization Script
# This script initializes the git repository and creates the initial commit

set -e  # Exit on any error

echo "🚀 Initializing DebugOpsMCP Git Repository..."

# Navigate to the project root
cd "$(dirname "$0")"
PROJECT_ROOT=$(pwd)

echo "📁 Project root: $PROJECT_ROOT"

# Initialize git repository if not already initialized
if [ ! -d ".git" ]; then
    echo "🔧 Initializing git repository..."
    git init
    
    # Set up initial configuration
    echo "⚙️ Setting up git configuration..."
    git config --local core.autocrlf true
    git config --local core.ignorecase false
    git config --local pull.rebase false
else
    echo "✅ Git repository already initialized"
fi

# Add all files to staging
echo "📦 Adding files to git staging area..."
git add .

# Check if there are any changes to commit
if git diff --cached --quiet; then
    echo "ℹ️ No changes to commit"
    exit 0
fi

# Create the initial commit
echo "💾 Creating initial commit..."
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

# Display commit information
echo ""
echo "✅ Initial commit created successfully!"
echo ""
git log --oneline -1
echo ""

# Display repository status
echo "📊 Repository Status:"
git status --porcelain | wc -l | xargs echo "   Files tracked:"
echo "   Branch: $(git branch --show-current)"
echo "   Latest commit: $(git log -1 --format='%h - %s')"

# Show next steps
echo ""
echo "🎯 Next Steps:"
echo "   1. Set up remote repository: git remote add origin <repository-url>"
echo "   2. Push to remote: git push -u origin main"
echo "   3. Set up branch protection rules"
echo "   4. Configure CI/CD workflows"
echo ""
echo "🔧 Development:"
echo "   1. Build: dotnet build core/DebugOpsMCP.sln"
echo "   2. Test: dotnet test core/"
echo "   3. Run: dotnet run --project core/src/DebugOpsMCP.Host"
echo ""

echo "🎉 DebugOpsMCP repository is ready for development!"