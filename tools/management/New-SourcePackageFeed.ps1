[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PublishedDirectory,
    [Parameter(Mandatory)][string]$ArtifactsPath,
    [Parameter(Mandatory)][string]$LogRoot,
    [ValidateSet('Debug','Release')][string]$Configuration='Release',
    [string]$PackageFallbackRoot,
    [string]$RuntimeIdentifier,
    [switch]$SingleFile,
    [switch]$NoBuild
)
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$published=(Resolve-Path -LiteralPath $PublishedDirectory).ProviderPath
$artifacts=[IO.Path]::GetFullPath($ArtifactsPath)
$logs=[IO.Path]::GetFullPath($LogRoot)
foreach($path in @($published,$artifacts,$logs)){
    if(!$path.StartsWith('F:\Seamless\',[StringComparison]::OrdinalIgnoreCase)){throw '配布先・ビルド出力・ログはF:\Seamless配下を指定してください。'}
}
if(!(Test-Path -LiteralPath (Join-Path $published 'Server.dll'))){throw '管理サーバーの配布出力が必要です。'}
$destination=Join-Path $published 'packages'
if((Test-Path -LiteralPath $destination) -or (Test-Path -LiteralPath $logs)){throw 'パッケージとログの保存先は未使用にしてください。'}
New-Item -ItemType Directory -Path $logs | Out-Null
$staging=Join-Path $published ('packages.stage-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $staging | Out-Null
$runner=Join-Path $repository 'TestSeamless/Invoke-LocalProcess.ps1'
$dotnet=(Get-Command dotnet).Source
$projects=@('Seamless')
$packageIds=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($name in $projects){[void]$packageIds.Add($name)}

function Invoke-FeedStage([string]$Name,[string]$Executable,[string[]]$CommandArguments){
    $result=& $runner -Executable $Executable -Arguments $CommandArguments -WorkingDirectory $repository -ArtifactDirectory (Join-Path $logs $Name) -TimeoutSeconds 600
    if($result.Outcome -ne 'Passed'){throw ('パッケージ作成の工程が失敗しました: '+$Name)}
}
function Read-PackageSpec([IO.Compression.ZipArchive]$Archive){
    $entries=@($Archive.Entries | Where-Object {$_.FullName.EndsWith('.nuspec',[StringComparison]::OrdinalIgnoreCase)})
    if($entries.Count -ne 1 -or $entries[0].Length -gt 1048576){throw 'パッケージ定義が不正です。'}
    $reader=[IO.StreamReader]::new($entries[0].Open())
    try{$text=$reader.ReadToEnd()}finally{$reader.Dispose()}
    $document=[xml]::new();$document.LoadXml($text)
    return ,$document
}
function Set-PackageVersion([xml]$Document,[string]$Version){
    $versionNode=$Document.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']/*[local-name()='version']")
    if($null -eq $versionNode){throw 'パッケージの版がありません。'}
    $versionNode.InnerText=$Version
    foreach($dependency in $Document.SelectNodes("//*[local-name()='dependency']")){
        $id=$dependency.GetAttribute('id')
        if($id -eq 'Seamless' -or $id.StartsWith('Seamless.',[StringComparison]::OrdinalIgnoreCase)){
            if(!$packageIds.Contains($id)){throw ('一式に不足する依存パッケージがあります: '+$id)}
            $dependency.SetAttribute('version','['+$Version+']')
        }
    }
}
function Get-PackageId([xml]$Document){
    $node=$Document.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']/*[local-name()='id']")
    if($null -eq $node -or !$packageIds.Contains($node.InnerText)){throw 'パッケージ識別子が不正です。'}
    return $node.InnerText
}

$arguments=@('build',(Join-Path $repository 'Seamless/Database/Server/Server.csproj'),'-c',$Configuration,'--artifacts-path',$artifacts,'-m:1','-p:UseSharedCompilation=false','-p:NuGetAudit=false')
$properties=@()
if($RuntimeIdentifier){$properties+="RuntimeIdentifier=$RuntimeIdentifier"}
if($SingleFile){
    if(!$RuntimeIdentifier){throw '単一EXEのSDK一式にはRuntimeIdentifierが必要です。'}
    $properties+=@('PublishSingleFile=true','SelfContained=true')
}
foreach($property in $properties){$arguments+="-p:$property"}
if($PackageFallbackRoot){$arguments+=('-p:RestoreFallbackFolders='+[IO.Path]::GetFullPath($PackageFallbackRoot))}
if(!$NoBuild){Invoke-FeedStage 'build-entry' $dotnet $arguments}
if($NoBuild){$properties+=("SeamlessSdkInputDirectory="+$published);$properties+='BuildProjectReferences=false'}
$plan=[ordered]@{
    Dotnet=$dotnet;Configuration=$Configuration;ArtifactsPath=$artifacts;Feed=$staging;Version='0.0.0';NoBuild=$true
    Sources='https://api.nuget.org/v3/index.json'
    Projects=@(Join-Path $repository 'Seamless/Database/Server/Server.csproj')
    Properties=$properties
}
$planPath=Join-Path $logs 'pack-plan.json'
[IO.File]::WriteAllText($planPath,($plan|ConvertTo-Json -Depth 4),[Text.UTF8Encoding]::new($false))
Invoke-FeedStage 'pack' (Get-Command pwsh).Source @('-NoProfile','-File',(Join-Path $repository 'TestSeamless/Pack-Products.ps1'),'-Plan',$planPath)

$payload=[Collections.Generic.List[string]]::new()
$assemblies=[Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
$found=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$packages=@(Get-ChildItem -LiteralPath $staging -Filter '*.nupkg' -File)
if($packages.Count -ne $projects.Count){throw '必要なパッケージ数が揃っていません。'}
foreach($package in $packages){
    $archive=[IO.Compression.ZipFile]::OpenRead($package.FullName)
    try{
        $spec=Read-PackageSpec $archive
        $id=Get-PackageId $spec
        if(!$found.Add($id)){throw 'パッケージ識別子が重複しています。'}
        Set-PackageVersion $spec 'COHORT'
        $specHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($spec.OuterXml)))
        $payload.Add($id+'/manifest='+$specHash)
        foreach($entry in $archive.Entries){
            $name=$entry.FullName
            if($name.EndsWith('/') -or $name.EndsWith('.nuspec') -or $name -eq '[Content_Types].xml' -or $name.StartsWith('_rels/') -or $name.StartsWith('package/services/metadata/')){continue}
            $stream=$entry.Open()
            try{$hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))}finally{$stream.Dispose()}
            $payload.Add($id+'/'+$name+'='+$hash)
            $file=[IO.Path]::GetFileName($name)
            if($name.StartsWith('lib/net10.0/',[StringComparison]::Ordinal)){$file=$name.Substring('lib/net10.0/'.Length)}
            if(($name.StartsWith('lib/',[StringComparison]::Ordinal) -or $name.StartsWith('analyzers/',[StringComparison]::Ordinal)) -and $file.EndsWith('.dll',[StringComparison]::Ordinal)){
                if($assemblies.ContainsKey($file) -and $assemblies[$file] -cne $hash){throw ('同名DLLの内容が異なります: '+$file)}
                $assemblies[$file]=$hash
                $runtime=Join-Path $published $file
                if((Test-Path -LiteralPath $runtime) -and (Get-FileHash -LiteralPath $runtime).Hash -cne $hash){throw ('配布サーバーとパッケージのDLLが異なります: '+$file)}
            }
        }
    }finally{$archive.Dispose()}
}
if(!$found.SetEquals($packageIds) -or !$assemblies.ContainsKey('Database.dll') -or !$assemblies.ContainsKey('Runtime.dll')){throw '一式の構成が不正です。'}
$payload.Sort([StringComparer]::Ordinal)
$fingerprint=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes([string]::Join("`n",$payload))))
$version='0.0.0-build.h'+$fingerprint.Substring(0,32).ToLowerInvariant()
$packageManifest=[Collections.Generic.List[object]]::new()
foreach($package in $packages){
    $archive=[IO.Compression.ZipFile]::Open($package.FullName,[IO.Compression.ZipArchiveMode]::Update)
    try{
        $spec=Read-PackageSpec $archive
        $id=Get-PackageId $spec
        Set-PackageVersion $spec $version
        $entry=@($archive.Entries | Where-Object {$_.FullName.EndsWith('.nuspec')})[0]
        $name=$entry.FullName;$entry.Delete()
        $writer=[IO.StreamWriter]::new($archive.CreateEntry($name).Open(),[Text.UTF8Encoding]::new($false))
        try{$writer.Write($spec.OuterXml)}finally{$writer.Dispose()}
        foreach($properties in @($archive.Entries | Where-Object {$_.FullName.EndsWith('.psmdcp')})){
            $reader=[IO.StreamReader]::new($properties.Open())
            try{$document=[xml]$reader.ReadToEnd()}finally{$reader.Dispose()}
            foreach($node in $document.SelectNodes("//*[local-name()='version']")){$node.InnerText=$version}
            $name=$properties.FullName;$properties.Delete()
            $writer=[IO.StreamWriter]::new($archive.CreateEntry($name).Open(),[Text.UTF8Encoding]::new($false))
            try{$writer.Write($document.OuterXml)}finally{$writer.Dispose()}
        }
    }finally{$archive.Dispose()}
    $file=$id+'.'+$version+'.nupkg'
    $renamed=Join-Path $staging $file
    Move-Item -LiteralPath $package.FullName -Destination $renamed
    $packageManifest.Add([ordered]@{id=$id;file=$file;sha256=(Get-FileHash -LiteralPath $renamed).Hash})
}
$manifest=[ordered]@{format=1;version=$version;fingerprint=$fingerprint;assemblies=$assemblies;packages=$packageManifest}
[IO.File]::WriteAllText((Join-Path $staging 'source-packages.json'),($manifest|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
Move-Item -LiteralPath $staging -Destination $destination
Write-Output ([pscustomobject]@{Version=$version;Packages=$packages.Count;Assemblies=$assemblies.Count;Path=$destination})
