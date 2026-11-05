[CmdletBinding()]
param([Parameter(Mandatory)][string]$PublishedDirectory,[Parameter(Mandatory)][string]$PackageRoot)
$ErrorActionPreference='Stop'
$destination=[IO.Path]::GetFullPath($PublishedDirectory);$packages=[IO.Path]::GetFullPath($PackageRoot)
$notices=Join-Path $destination 'ThirdParty';New-Item -ItemType Directory -Path $notices -Force | Out-Null
$inventory=@();$seen=@{}
foreach($deps in Get-ChildItem -LiteralPath $destination -Filter '*.deps.json' -File){
    $document=Get-Content -LiteralPath $deps.FullName -Raw | ConvertFrom-Json
    foreach($target in $document.targets.PSObject.Properties){foreach($entry in $target.Value.PSObject.Properties){
        $library=$document.libraries.PSObject.Properties[$entry.Name].Value
        if($library.type -ne 'package' -or $seen.ContainsKey($entry.Name)){continue}
        $assets=@($entry.Value.runtime.PSObject.Properties.Name)+@($entry.Value.native.PSObject.Properties.Name)+@($entry.Value.runtimeTargets.PSObject.Properties.Name)
        $shipped=@($assets | Where-Object { $_ -and (Get-ChildItem -LiteralPath $destination -Recurse -File -Filter ([IO.Path]::GetFileName($_)) | Select-Object -First 1) })
        if(!$shipped.Count){continue}
        $seen[$entry.Name]=$true
        $package=[IO.Path]::GetFullPath((Join-Path $packages $library.path))
        if(!$package.StartsWith($packages.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Package path escapes the configured cache.'}
        $nuspec=Get-ChildItem -LiteralPath $package -Filter '*.nuspec' -File | Select-Object -First 1
        if(!$nuspec){throw ('Package metadata missing: '+$entry.Name)}
        [xml]$xml=Get-Content -LiteralPath $nuspec.FullName -Raw;$metadata=$xml.package.metadata
        $license='';if($metadata.license){$license=$metadata.license.InnerText;if(!$license){$license=[string]$metadata.license}}
        $files=@(Get-ChildItem -LiteralPath $package -File | Where-Object Name -match 'license|notice|copyright')
        if($metadata.license.type -eq 'file'){$licenseFile=[IO.Path]::GetFullPath((Join-Path $package $license));if(!$licenseFile.StartsWith($package.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'License path escapes the package.'};$files+=Get-Item -LiteralPath $licenseFile}
        $copied=@();foreach($file in $files | Sort-Object FullName -Unique){$folder=Join-Path $notices ($entry.Name.Replace('/','.'));New-Item -ItemType Directory -Path $folder -Force | Out-Null;Copy-Item -LiteralPath $file.FullName -Destination $folder;$copied+=[IO.Path]::GetRelativePath($destination,(Join-Path $folder $file.Name))}
        if($license -eq 'Apache-2.0'){$folder=Join-Path $notices ($entry.Name.Replace('/','.'));New-Item -ItemType Directory -Path $folder -Force | Out-Null;Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Licenses\Apache-2.0.txt') -Destination $folder;$copied+=[IO.Path]::GetRelativePath($destination,(Join-Path $folder 'Apache-2.0.txt'))}
        if($entry.Name -eq 'diskann-garnet/8.0.0'){$folder=Join-Path $notices 'diskann-garnet.8.0.0';New-Item -ItemType Directory -Path $folder -Force | Out-Null;foreach($name in @('DiskANN-LICENSE.txt','DiskANN-NOTICE.txt')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('Licenses\'+$name)) -Destination $folder;$copied+=[IO.Path]::GetRelativePath($destination,(Join-Path $folder $name))}}
        $inventory+=[ordered]@{Package=$entry.Name;License=$license;LicenseUrl=[string]$metadata.licenseUrl;Copyright=[string]$metadata.copyright;Authors=[string]$metadata.authors;Project=[string]$metadata.projectUrl;Notices=$copied}
    }}
}
$inventory | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $notices 'packages.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Licenses\sources.json') -Destination (Join-Path $notices 'upstream-sources.json')
$text=@('# Third-party notices','','実際のpublish出力に含まれるNuGet実行時資産の一覧。パッケージ同梱のライセンス・通知をThirdPartyへ保持する。著作者・権利表示・ライセンス指定はpackages.jsonの元メタデータを参照。','','ASP.NET Core/.NETはframework-dependent配布に同梱しない。別途配布する場合はランタイム自身のライセンス・第三者通知も保持する。','','| Package | License | Source |','|---|---|---|')
foreach($entry in $inventory){$text+='| '+$entry.Package+' | '+$entry.License+' | '+$entry.Project+' |'}
$text+=@('','MITを指定するパッケージの許諾条文（各権利表示はpackages.jsonを参照）:','')
$mit=Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\..\LICENSE');$text+=($mit | Select-Object -Skip 3)
$text | Set-Content -LiteralPath (Join-Path $destination 'THIRD-PARTY-NOTICES.md') -Encoding utf8
