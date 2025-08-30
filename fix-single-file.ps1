param(
    [string]$FilePath
)

$content = Get-Content $FilePath -Raw

# Replace return statements with Task.FromResult wrappers
$patterns = @{
    'return new McpErrorResponse' = 'return Task.FromResult<McpResponse>(new McpErrorResponse'
    'return new DebugThreadsResponse' = 'return Task.FromResult<McpResponse>(new DebugThreadsResponse'
    'return new DebugStatusResponse' = 'return Task.FromResult<McpResponse>(new DebugStatusResponse'
    'return new DebugVariablesResponse' = 'return Task.FromResult<McpResponse>(new DebugVariablesResponse'
    'return new DebugEvaluateResponse' = 'return Task.FromResult<McpResponse>(new DebugEvaluateResponse'
}

foreach ($pattern in $patterns.GetEnumerator()) {
    $content = $content.Replace($pattern.Key, $pattern.Value)
}

# Fix the closing parentheses by replacing }; patterns at the end of return blocks
# This is a simple regex to find method-ending return statements
$content = $content -replace '(\s+});\s*(\r?\n\s*}\s*(\r?\n\s*catch|\r?\n\s*$))', '$1);$2'

Set-Content $FilePath $content -NoNewline
Write-Host "Fixed $FilePath"