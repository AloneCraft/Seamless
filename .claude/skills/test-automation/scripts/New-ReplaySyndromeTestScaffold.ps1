param(
    [string]$ProjectRoot = ".",
    [ValidateSet("EditMode", "PlayMode", "System", "All")]
    [string]$Layer = "All"
)

$ErrorActionPreference = "Stop"

$resolvedRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
$testsRoot = Join-Path $resolvedRoot "Assets/_Project/Tests"

$layers = if ($Layer -eq "All") {
    @("EditMode", "PlayMode", "System")
} else {
    @($Layer)
}

foreach ($item in $layers) {
    $path = Join-Path $testsRoot $item
    if (-not (Test-Path -LiteralPath $path)) {
        New-Item -ItemType Directory -Path $path | Out-Null
        Write-Host "Created $path"
    } else {
        Write-Host "Exists   $path"
    }
}

Write-Host "Done. Let Unity generate .meta files."
