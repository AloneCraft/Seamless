$dirs = @('Seamless', 'Seamless.Database', 'Seamless.Linq', 'Tests\Seamless.TestKit')
foreach ($d in $dirs) {
    if (Test-Path $d) {
        $files = Get-ChildItem -Path $d -Recurse -File -Filter '*.cs' -ErrorAction SilentlyContinue
        foreach ($f in $files) {
            if ($f.FullName -match '\\(obj|bin)\\\\') { continue }
            $m = Select-String -Path $f.FullName -Pattern 'AssertQuery'
            if ($m) {
                Write-Host $f.FullName
                foreach ($x in $m) { Write-Host "  $($x.LineNumber): $($x.Line.Trim())" }
            }
        }
    }
}
