# Fix all Task.FromResult issues at once
$ErrorActionPreference = "Stop"

# Define the files and their specific fixes
$fixes = @(
    @{
        File = "core\src\DebugOpsMCP.Core\Tools\DebugInspectionTool.cs"
        Patterns = @(
            @{ Find = "return new McpErrorResponse"; Replace = "return Task.FromResult<McpResponse>(new McpErrorResponse" },
            @{ Find = "return new DebugStackTraceResponse"; Replace = "return Task.FromResult<McpResponse>(new DebugStackTraceResponse" },
            @{ Find = "return new DebugVariablesResponse"; Replace = "return Task.FromResult<McpResponse>(new DebugVariablesResponse" },
            @{ Find = "return new DebugEvaluateResponse"; Replace = "return Task.FromResult<McpResponse>(new DebugEvaluateResponse" }
        )
    },
    @{
        File = "core\src\DebugOpsMCP.Core\Tools\DebugThreadTool.cs"
        Patterns = @(
            @{ Find = "return new McpErrorResponse"; Replace = "return Task.FromResult<McpResponse>(new McpErrorResponse" },
            @{ Find = "return new DebugThreadsResponse"; Replace = "return Task.FromResult<McpResponse>(new DebugThreadsResponse" },
            @{ Find = "return new DebugStatusResponse"; Replace = "return Task.FromResult<McpResponse>(new DebugStatusResponse" }
        )
    }
)

foreach ($filefix in $fixes) {
    if (Test-Path $filefix.File) {
        Write-Host "Processing $($filefix.File)"
        
        $content = Get-Content $filefix.File -Raw
        
        foreach ($pattern in $filefix.Patterns) {
            $content = $content.Replace($pattern.Find, $pattern.Replace)
        }
        
        # Also need to close the extra parenthesis - fix common pattern of return statement blocks
        $content = $content -replace '(\s+});\s*(\r?\n\s*})', '$1);$2'
        
        Set-Content $filefix.File $content -NoNewline
        Write-Host "Fixed $($filefix.File)"
    } else {
        Write-Host "File not found: $($filefix.File)"
    }
}

Write-Host "All fixes applied!"