$sourceDir = "Execution.Tests"
$destDir = "Tests\Seamless.Core.Tests"
$files = Get-ChildItem -Path $sourceDir -File -Filter "*.cs"
foreach ($f in $files) {
    Copy-Item -Path $f.FullName -Destination (Join-Path $destDir $f.Name) -Force
    Write-Host "Copied: $($f.Name)"
}
