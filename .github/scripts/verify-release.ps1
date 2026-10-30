# リリース成果物(.nupkg / .snupkg / サンプル ZIP)を検証する。release.yml と手元の両方から使う。
param(
    [Parameter(Mandatory)][string]$Dir,
    [Parameter(Mandatory)][string]$Version
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$packages = @(
    'Seamless.Collections',
    'Seamless.Execution',
    'Seamless.Collections.Persisted',
    'Seamless.Tsavorite.Core',
    'Seamless.Collections.Tsavorite',
    'Seamless.Linq',
    'Seamless.Transactions',
    'Seamless.Transactions.Collections',
    'Seamless.Database',
    'Seamless.Database.Generator',
    'Seamless.Database.Rdbms',
    'Seamless.FLinq.Generator',
    'Seamless',
    'Seamless.Database.SqlServer',
    'Seamless.Database.Tool',
    'Seamless.Database.Client',
    'Seamless.Database.Query',
    'Seamless.Database.Gateway'
)
$forbidden = '(^|/)(bin|obj|Library|Temp|Logs|Work|TestResults|\.git|\.vs)/'
$errors = [System.Collections.Generic.List[string]]::new()

function Open-Zip([string]$path) { [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $path).Path) }

foreach ($id in $packages) {
    $file = Join-Path $Dir "$id.$Version.nupkg"
    if (-not (Test-Path -LiteralPath $file)) { $errors.Add("missing: $id.$Version.nupkg"); continue }
    if ((Get-Item -LiteralPath $file).Length -lt 4096) { $errors.Add("too small: $file") }
    $zip = Open-Zip $file
    try {
        $names = $zip.Entries.FullName
        $nuspec = $zip.Entries | Where-Object { $_.FullName -like '*.nuspec' } | Select-Object -First 1
        if (-not $nuspec) { $errors.Add("no nuspec: $id"); continue }
        $reader = [System.IO.StreamReader]::new($nuspec.Open())
        try { [xml]$xml = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $meta = $xml.package.metadata
        if ($meta.id -ne $id) { $errors.Add("nuspec id mismatch: $($meta.id) != $id") }
        if ($meta.version -ne $Version) { $errors.Add("nuspec version mismatch in ${id}: $($meta.version) != $Version") }
        foreach ($dependency in $xml.SelectNodes("//*[local-name()='dependency']")) {
            $dependencyId = $dependency.GetAttribute('id')
            if ($dependencyId -eq 'Seamless' -or $dependencyId.StartsWith('Seamless.', [StringComparison]::Ordinal)) {
                $dependencyFile = Join-Path $Dir "$dependencyId.$Version.nupkg"
                if (-not (Test-Path -LiteralPath $dependencyFile)) {
                    $errors.Add("missing release dependency: $dependencyId.$Version.nupkg required by $id ($($dependency.GetAttribute('version')))")
                }
            }
        }
        $readme = 'README.md'
        if ($id -eq 'Seamless.Tsavorite.Core') { $readme = 'README.Core.md' }
        if ($names -notcontains $readme) { $errors.Add("no $readme in $id") }
        if ($id -eq 'Seamless.Tsavorite.Core') {
            foreach ($required in @('lib/net10.0/Tsavorite.core.dll','lib/net10.0/Garnet.common.dll','lib/net10.0/Tsavorite.devices.AzureStorageDevice.dll','buildTransitive/Seamless.Tsavorite.Core.targets','UPSTREAM-PROVENANCE.json','recovery-head-pages.patch','licenses/LICENSE')) {
                if ($names -notcontains $required) { $errors.Add("missing patched Core entry: $required") }
            }
            $managed = @($names | Where-Object { $_ -like 'lib/net10.0/*.dll' })
            if ($managed.Count -ne 3) { $errors.Add("patched Core must contain exactly 3 managed DLLs") }
            $native = @($names | Where-Object { $_ -like 'runtimes/*/native/*native_device*' })
            if ($native.Count -ne 10) { $errors.Add("patched Core must retain exactly 10 native device files") }
            if ($meta.dependencies.group.dependency.id -contains 'Microsoft.Garnet') { $errors.Add('patched Core must not depend on original Microsoft.Garnet') }
        }
        $bad = $names | Where-Object { $_ -match $forbidden }
        if ($bad) { $errors.Add("forbidden entries in ${id}: $($bad -join ', ')") }
    } finally { $zip.Dispose() }
}

$samples = Join-Path $Dir "Seamless-Samples-$Version.zip"
if (-not (Test-Path -LiteralPath $samples)) { $errors.Add("missing: Seamless-Samples-$Version.zip") }
else {
    if ((Get-Item -LiteralPath $samples).Length -lt 4096) { $errors.Add("too small: $samples") }
    $zip = Open-Zip $samples
    try {
        $names = $zip.Entries.FullName
        if (-not ($names | Where-Object { $_ -like 'Samples/Sample.NetShop/*' })) { $errors.Add('samples zip lacks Samples/Sample.NetShop') }
        $bad = $names | Where-Object { $_ -match $forbidden }
        if ($bad) { $errors.Add("forbidden entries in samples zip: $(($bad | Select-Object -First 5) -join ', ')") }
    } finally { $zip.Dispose() }
}

if ($errors.Count) { $errors | ForEach-Object { Write-Error $_ -ErrorAction Continue }; exit 1 }
Write-Host "OK: $($packages.Count) packages + samples verified for $Version"
