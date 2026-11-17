# リリース成果物(.nupkg / .snupkg / サンプル ZIP)を検証する。release.yml と手元の両方から使う。
param(
    [Parameter(Mandatory)][string]$Dir,
    [Parameter(Mandatory)][string]$Version
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$packages = @('Seamless')
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
        if ($names -notcontains 'README.md') { $errors.Add("no README.md in $id") }
        foreach ($required in @(
            'lib/net10.0/Runtime.dll','lib/net10.0/Database.dll','lib/net10.0/Client.dll','lib/net10.0/Server.dll',
            'lib/net10.0/en/Server.resources.dll','lib/net10.0/Tsavorite.core.dll','lib/net10.0/Garnet.common.dll',
            'lib/net10.0/Tsavorite.devices.AzureStorageDevice.dll','lib/net10.0/Sample.NetShop.Core.dll',
            'analyzers/dotnet/cs/Seamless.FLinq.Generator.dll','analyzers/dotnet/cs/Seamless.Database.Generator.dll',
            'buildTransitive/Seamless.props','buildTransitive/Seamless.targets','buildTransitive/Seamless.Tsavorite.Core.targets',
            'licenses/UPSTREAM-PROVENANCE.json','licenses/recovery-head-pages.patch','licenses/Tsavorite')) {
            if ($names -notcontains $required) { $errors.Add("missing SDK entry: $required") }
        }
        $managed = @($names | Where-Object { $_ -match '^lib/net10\.0/[^/]+\.dll$' })
        if ($managed.Count -ne 8) { $errors.Add('Seamless SDK must contain exactly 8 root runtime DLLs') }
        $native = @($names | Where-Object { $_ -like 'runtimes/*/native/*native_device*' })
        if ($native.Count -ne 10) { $errors.Add('Seamless SDK must retain exactly 10 native device files') }
        if ($meta.dependencies.group.dependency.id -contains 'Microsoft.Garnet') { $errors.Add('Seamless SDK must not depend on original Microsoft.Garnet') }
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
