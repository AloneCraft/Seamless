$files = Get-ChildItem -Path @('Benchmark.Dynamic.Tests', 'Seamless.Linq', 'Seamless.Core', 'Seamless') -Recurse -File -Filter '*.cs' -ErrorAction SilentlyContinue
foreach ($f in $files) {
    if ($f.FullName -match '\\(obj|bin)\\\\') { continue }
    $match = Select-String -Path $f.FullName -Pattern 'class FixedCaseCatalog|EffectiveIdealBody|GroupBy.*Count' -SimpleMatch:$false
    if ($match) {
        Write-Host $f.FullName
        foreach ($m in $match) {
            Write-Host "  Line $($m.LineNumber): $($m.Line.Trim())"
        }
    }
}
