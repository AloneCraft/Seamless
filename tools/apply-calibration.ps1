# Apply calibrated Count values (per-method) to Benchmark*.cs
$ErrorActionPreference='Stop'
$dictFile = "$PSScriptRoot\..\tests\Seamless.Benchmarks\Benchmark\BenchmarkDictionary.cs"
$setFile  = "$PSScriptRoot\..\tests\Seamless.Benchmarks\Benchmark\BenchmarkHashSet.cs"

# method name ↁEcalibrated Count
$dict = [ordered]@{
    'Add'                                 = 2500000
    'Remove_key'                          = 2300000
    'Remove_key_value'                    = 2300000
    'Clear'                               = 5000000
    'TryGetValue'                         = 2300000
    'Contains'                            = 2000000
    'ContainsKey'                         = 2400000
    'Enumerate_foreach'                   = 1500000
    'Count_'                              = 1000
    'Keys_'                               = 1000
    'Values_'                             = 1000
    'ToList_'                             = 700000
    'Indexer_Get'                         = 2500000
    'Indexer_Set'                         = 2500000
    'IDictionary_Add'                     = 2400000
    'IDictionary_ContainsKey'             = 2700000
    'IDictionary_Remove'                  = 2500000
    'IDictionary_TryGetValue'             = 2700000
    'IDictionary_Keys'                    = 1000
    'IDictionary_Values'                  = 1000
    'ICollection_Add'                     = 2700000
    'ICollection_Clear'                   = 5000000
    'ICollection_Contains'                = 2800000
    'ICollection_CopyTo'                  = 700000
    'ICollection_Remove'                  = 2500000
    'IReadOnlyCollection_Count'           = 1000
    'IReadOnlyDictionary_Indexer_Get'     = 2600000
    'IReadOnlyDictionary_ContainsKey'     = 2600000
    'IReadOnlyDictionary_TryGetValue'     = 2500000
    'IReadOnlyDictionary_Keys'            = 5000000
    'IReadOnlyDictionary_Values'          = 5000000
    'IEnumerable_GetEnumerator'           = 1400000
}
$set = [ordered]@{
    'Add'                 = 3600000
    'Remove'              = 3200000
    'Contains'            = 3500000
    'CopyTo'              = 800000
    'Enumerate_foreach'   = 1600000
    'Count_'              = 1000
    'Clear'               = 5000000
    'IntersectWith'       = 640000
    'SymmetricExceptWith' = 740000
    'UnionWith'           = 900000
    'ExceptWith'          = 700000
    'IsProperSubsetOf'    = 1700000
    'IsProperSupersetOf'  = 1700000
    'IsSubsetOf'          = 2000000
    'IsSupersetOf'        = 1700000
    'Overlaps'            = 2000000
    'SetEquals'           = 1700000
    'ICollection_Add'     = 3800000
    'ICollection_Clear'   = 5000000
    'ICollection_Contains'= 3100000
    'ICollection_CopyTo'  = 800000
    'ICollection_Remove'  = 3300000
    'IEnumerable_GetEnumerator' = 1600000
}

function Apply-Counts($path, $map) {
    $text = Get-Content -Raw -LiteralPath $path
    foreach($k in $map.Keys) {
        $count = $map[$k]
        $countLit = ('{0:N0}' -f $count) -replace ',','_'
        # match: public void <name>(... ) {...const int Count = 100000 * BenchmarkScale.Scale;
        $pattern = '(?s)(public void '+[Regex]::Escape($k)+'\([^)]*\)\s*\{[^}]*?const int Count = )[0-9_]+( \* BenchmarkScale\.Scale;)'
        $new = '${1}'+$countLit+'${2}'
        $updated = [Regex]::Replace($text, $pattern, $new)
        if($updated -eq $text) { Write-Warning "no-match: $k"; continue }
        $text = $updated
        Write-Host "  $k -> $countLit"
    }
    Set-Content -LiteralPath $path -Value $text -NoNewline
}

Write-Host "# Dictionary"
Apply-Counts $dictFile $dict
Write-Host "# HashSet"
Apply-Counts $setFile $set
Write-Host "Done."
