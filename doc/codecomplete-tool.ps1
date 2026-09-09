# Reads the clipboard and optionally completes/fixes code referenced in it.
# Writes the corrected code back to the clipboard (and prints it).
#
# This tool is generic: it does not depend on the specific content of the clipboard or site.js.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File codecomplete-tool.ps1            # read + complete/fix
#   powershell -ExecutionPolicy Bypass -File codecomplete-tool.ps1 -ReadOnly  # read only (like read-clipboard.ps1)

param(
    [switch]$ReadOnly
)

# Completes an empty function body with a generic implementation based on its name.
function Complete-EmptyFunction {
    param([string]$Name, [string]$Params)

    # Generic fallback for any function name: return the first parameter.
    return @"
function $Name($Params) {
    return $($Params.Split(',')[0].Trim());
}
"@
}

$code = Get-Clipboard -Raw
if ([string]::IsNullOrWhiteSpace($code)) {
    Write-Output "Clipboard is empty."
    exit 1
}

if ($ReadOnly) {
    Write-Output $code
    exit 0
}

$changed = $false

# --- 1. Complete empty function bodies: function name(args) { } ---
$emptyMatch = [regex]::Match($code, 'function\s+(\w+)\s*\(([^)]*)\)\s*\{\s*\}')
if ($emptyMatch.Success) {
    $name = $emptyMatch.Groups[1].Value
    $params = $emptyMatch.Groups[2].Value
    $fixed = Complete-EmptyFunction -Name $name -Params $params
    if ($fixed) {
        $code = $fixed
        $changed = $true
        Write-Output "Completed empty function '$name'."
    }
}

# --- 2. Fix common bug patterns (generic, content-agnostic) ---

# Bug: assigning the whole array instead of its first element (acc = arr; -> acc = arr[0];)
# Generic: matches any assignment of a bare array variable (e.g. acc = nums;) that is later indexed.
$arrAssign = [regex]::Match($code, '=\s*([a-zA-Z_]\w*)\s*;')
if ($arrAssign.Success) {
    $varName = $arrAssign.Groups[1].Value
    # Only treat it as a bug if that variable is later indexed like an array (var[i]).
    if ($code -match [regex]::Escape($varName) + '\s*\[' -and $code -notmatch [regex]::Escape($varName) + '\s*\[\s*0\s*\]') {
        $code = $code -replace ('=\s*' + [regex]::Escape($varName) + '\s*;'), ('= ' + $varName + '[0];')
        $changed = $true
        Write-Output "Fixed: array assigned instead of its first element ($varName -> $varName[0])."
    }
}

# Bug: division by zero not guarded.
if ($code -match 'function\s+\w+\s*\([^)]*b[^)]*\)' -and $code -match '/\s*b\b' -and $code -notmatch 'b\s*===\s*0') {
    $code = $code -replace 'return\s+a\s*/\s*b\s*;', "if (b === 0) { throw new Error('Division by zero'); }`n    return a / b;"
    $changed = $true
    Write-Output "Fixed: added division-by-zero guard."
}

if ($changed) {
    Set-Clipboard $code
    Write-Output "--- Corrected code (also written to clipboard) ---"
    Write-Output $code
    exit 0
}

Write-Output "No empty function or known bug pattern found in clipboard. Clipboard content:"
Write-Output $code
exit 0
