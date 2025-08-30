# PowerShell script to fix Task return issues
# This script wraps return statements with Task.FromResult for methods that should return Task<T>

$files = @(
    "core\src\DebugOpsMCP.Core\Tools\DebugInspectionTool.cs",
    "core\src\DebugOpsMCP.Core\Tools\DebugThreadTool.cs"
)

foreach ($file in $files) {
    if (Test-Path $file) {
        Write-Host "Processing $file"
        
        $content = Get-Content $file -Raw
        
        # Replace return statements that need Task.FromResult wrapping
        $content = $content -replace 'return new McpErrorResponse', 'return Task.FromResult<McpResponse>(new McpErrorResponse'
        $content = $content -replace 'return new Debug(\w+)Response', 'return Task.FromResult<McpResponse>(new Debug$1Response'
        
        # Fix the closing braces - add extra ) for Task.FromResult
        $content = $content -replace '(\s+});\s*$', '$1);'
        
        Set-Content $file $content
        Write-Host "Fixed $file"
    }
}

Write-Host "Done fixing Task return statements"