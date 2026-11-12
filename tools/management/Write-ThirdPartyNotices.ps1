[CmdletBinding()]
param([Parameter(Mandatory)][string]$PublishedDirectory,[Parameter(Mandatory)][string[]]$PackageRoot,[string]$PackageFallbackRoot,[string]$AppHostSourcePath,[string]$ToolEntryAssembly)
$ErrorActionPreference='Stop'
$destination=[IO.Path]::GetFullPath($PublishedDirectory);if($PackageFallbackRoot){$PackageRoot+= $PackageFallbackRoot};$packageRoots=@($PackageRoot | ForEach-Object {[IO.Path]::GetFullPath($_)})
$separator=[IO.Path]::DirectorySeparatorChar
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
        $package=$null;$nuspec=$null
        foreach($packages in $packageRoots){
            $candidate=[IO.Path]::GetFullPath((Join-Path $packages $library.path))
            if(!$candidate.StartsWith($packages.TrimEnd($separator)+$separator,[StringComparison]::OrdinalIgnoreCase)){throw 'Package path escapes the configured cache.'}
            if(!(Test-Path -LiteralPath $candidate -PathType Container)){continue}
            $nuspec=Get-ChildItem -LiteralPath $candidate -Filter '*.nuspec' -File | Select-Object -First 1
            if($nuspec){$package=$candidate;break}
        }
        if(!$nuspec){throw ('Package metadata missing: '+$entry.Name)}
        [xml]$xml=Get-Content -LiteralPath $nuspec.FullName -Raw;$metadata=$xml.package.metadata
        $license='';if($metadata.license){$license=$metadata.license.InnerText;if(!$license){$license=[string]$metadata.license}}
        $files=@(Get-ChildItem -LiteralPath $package -File | Where-Object Name -match 'license|notice|copyright')
        if($metadata.license.type -eq 'file'){$licenseFile=[IO.Path]::GetFullPath((Join-Path $package $license));if(!$licenseFile.StartsWith($package.TrimEnd($separator)+$separator,[StringComparison]::OrdinalIgnoreCase)){throw 'License path escapes the package.'};$files+=Get-Item -LiteralPath $licenseFile}
        $copied=@();foreach($file in $files | Sort-Object FullName -Unique){$folder=Join-Path $notices ($entry.Name.Replace('/','.'));New-Item -ItemType Directory -Path $folder -Force | Out-Null;Copy-Item -LiteralPath $file.FullName -Destination $folder;$copied+=[IO.Path]::GetRelativePath($destination,(Join-Path $folder $file.Name))}
        if($license -eq 'Apache-2.0'){$folder=Join-Path $notices ($entry.Name.Replace('/','.'));New-Item -ItemType Directory -Path $folder -Force | Out-Null;Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Licenses\Apache-2.0.txt') -Destination $folder;$copied+=[IO.Path]::GetRelativePath($destination,(Join-Path $folder 'Apache-2.0.txt'))}
        if($entry.Name.StartsWith('diskann-garnet/',[StringComparison]::OrdinalIgnoreCase)){
            if($entry.Name -notin 'diskann-garnet/8.0.0','diskann-garnet/9.0.0'){throw ('DiskANNのライセンスと通知の照合が必要です: '+$entry.Name)}
            $license='MIT'
            $folder=Join-Path $notices ($entry.Name.Replace('/','.'))
            New-Item -ItemType Directory -Path $folder -Force|Out-Null
            foreach($name in @('DiskANN-LICENSE.txt','DiskANN-NOTICE.txt')){
                Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('Licenses\'+$name)) -Destination $folder
                $copied+=[IO.Path]::GetRelativePath($destination,(Join-Path $folder $name))
            }
        }
        $inventory+=[ordered]@{Package=$entry.Name;License=$license;LicenseUrl=[string]$metadata.licenseUrl;Copyright=[string]$metadata.copyright;Authors=[string]$metadata.authors;Project=[string]$metadata.projectUrl;Notices=$copied}
    }}
}
ConvertTo-Json -InputObject @($inventory) -Depth 6 | Set-Content -LiteralPath (Join-Path $notices 'packages.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Licenses\sources.json') -Destination (Join-Path $notices 'upstream-sources.json')
$hosts=@(foreach($deps in Get-ChildItem -LiteralPath $destination -Filter '*.deps.json' -File){
    $stem=$deps.Name.Substring(0,$deps.Name.Length-'.deps.json'.Length)
    if($ToolEntryAssembly -and $stem -eq $ToolEntryAssembly){continue}
    if(!(Test-Path -LiteralPath (Join-Path $destination ($stem+'.dll'))) -or !(Test-Path -LiteralPath (Join-Path $destination ($stem+'.runtimeconfig.json')))){continue}
    foreach($name in @(($stem+'.exe'),$stem)){$path=Join-Path $destination $name;if(Test-Path -LiteralPath $path -PathType Leaf){[pscustomobject]@{Path=$name;Sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}}}
})
if($hosts.Count){
    if(!$AppHostSourcePath -or !(Test-Path -LiteralPath $AppHostSourcePath -PathType Leaf)){throw 'Apphostを含む配布には、SDKが解決したAppHostSourcePathを指定してください。'}
    $hash=(Get-FileHash -LiteralPath $AppHostSourcePath -Algorithm SHA256).Hash
    $catalog=@(Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Licenses/AppHost.sources.json') -Raw|ConvertFrom-Json)
    $origin=@($catalog|Where-Object AppHostSourceSha256 -eq $hash)
    if($origin.Count -ne 1){throw '選択したapphostの通知が未登録です。公式パッケージとSDK元ファイルを照合してAppHost.sources.jsonを更新してください。'}
    $origin=$origin[0];$folder=Join-Path $notices ($origin.Package+'.'+$origin.Version);New-Item -ItemType Directory -Path $folder -Force|Out-Null
    $licenseRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'Licenses')).TrimEnd($separator)+$separator
    $copied=@(foreach($file in $origin.Notices){
        $path=[IO.Path]::GetFullPath((Join-Path $licenseRoot $file.Path))
        if(!$path.StartsWith($licenseRoot,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($file.Name) -ne $file.Name){throw 'Apphost notice path escapes its source directory.'}
        if((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.Sha256){throw 'Apphost notice checksum mismatch.'}
        $target=Join-Path $folder $file.Name;Copy-Item -LiteralPath $path -Destination $target
        [pscustomobject]@{Path=[IO.Path]::GetRelativePath($destination,$target);Sha256=$file.Sha256}
    })
    $hostRecord=[ordered]@{Package=$origin.Package;Version=$origin.Version;Runtime=$origin.Runtime;Source=$origin.Source;ArchiveSha256=$origin.ArchiveSha256;AppHostSourceSha256=$hash;PublishedAppHosts=$hosts;Notices=$copied}
    if($ToolEntryAssembly){$hostRecord.ToolEntryAssembly=$ToolEntryAssembly}
    $hostRecord|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $notices 'apphost.json') -Encoding utf8
}
$text=@('# Third-party notices','','実際のpublish出力に含まれるNuGet実行時資産の一覧。パッケージ同梱のライセンス・通知をThirdPartyへ保持する。著作者・権利表示・ライセンス指定はpackages.jsonの元メタデータを参照。','','共有の.NET/ASP.NET Coreランタイム本体はframework-dependent配布に同梱しない。起動用apphostを含む場合は、その元バイナリと一致する公式パッケージの通知と出所をThirdParty/apphost.jsonに記録する。別途ランタイムを配布する場合は、そのランタイム自身のライセンス・第三者通知も保持する。','','| Package | License | Source |','|---|---|---|')
if($hosts.Count){$text+='| '+$origin.Package+'/'+$origin.Version+' (apphost) | 同梱LICENSE.TXT参照 | '+$origin.Source+' |'}
foreach($entry in $inventory){$text+='| '+$entry.Package+' | '+$entry.License+' | '+$entry.Project+' |'}
$text+=@('','MITを指定するパッケージの許諾条文（各権利表示はpackages.jsonを参照）:','')
$mit=Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\..\LICENSE');$text+=($mit | Select-Object -Skip 3)
$text | Set-Content -LiteralPath (Join-Path $destination 'THIRD-PARTY-NOTICES.md') -Encoding utf8
