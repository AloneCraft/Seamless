[CmdletBinding()]
param([Parameter(Mandatory)][string]$PublishedDirectory)
$ErrorActionPreference='Stop'
$directory=(Resolve-Path -LiteralPath $PublishedDirectory).ProviderPath
$paths=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach($deps in Get-ChildItem -LiteralPath $directory -Filter '*.deps.json' -File){
    $document=Get-Content -LiteralPath $deps.FullName -Raw | ConvertFrom-Json
    foreach($target in $document.targets.PSObject.Properties){
        foreach($library in $target.Value.PSObject.Properties){
            foreach($asset in $library.Value.native.PSObject.Properties.Name){
                if($asset -and $asset.StartsWith('runtimes/',[StringComparison]::Ordinal)){
                    if($asset -notmatch '^runtimes/[A-Za-z0-9.-]+/native/[A-Za-z0-9_.-]+$'){throw 'ネイティブ資産の配置パスが不正です。'}
                    [void]$paths.Add($asset)
                }
            }
        }
    }
}
# TsavoriteはP/Invokeにruntimes配下の相対名を指定する。publishの平坦化後も同じ配置を提供する。
foreach($asset in $paths){
    $source=Join-Path $directory ([IO.Path]::GetFileName($asset))
    $destination=Join-Path $directory $asset
    if(!(Test-Path -LiteralPath $source -PathType Leaf)){throw ('ネイティブ資産が配布出力にありません: '+$asset)}
    if(Test-Path -LiteralPath $destination){
        if((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $destination).Hash){throw ('ネイティブ資産の配置が競合しています: '+$asset)}
    }else{
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $destination
    }
}
