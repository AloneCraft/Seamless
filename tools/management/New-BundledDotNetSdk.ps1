[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Destination,
    [string]$DotNetRoot="$env:ProgramFiles\dotnet",
    [string]$SdkVersion='10.0.404'
)
$ErrorActionPreference='Stop'
$destinationPath=[IO.Path]::GetFullPath($Destination)
if(!$destinationPath.StartsWith('F:\Seamless\',[StringComparison]::OrdinalIgnoreCase)){
    throw 'SDKの保存先はF:\Seamless配下を指定してください。'
}
if(Test-Path -LiteralPath $destinationPath){throw 'SDKの保存先には未使用のディレクトリを指定してください。'}
$sourceRoot=(Resolve-Path -LiteralPath $DotNetRoot).ProviderPath
$sdk=Join-Path $sourceRoot "sdk/$SdkVersion"
$config=Get-Content -LiteralPath (Join-Path $sdk 'dotnet.runtimeconfig.json') -Raw|ConvertFrom-Json
$runtimeVersion=$config.runtimeOptions.framework.version
$directories=@(
    "sdk/$SdkVersion",
    "host/fxr/$runtimeVersion",
    "shared/Microsoft.NETCore.App/$runtimeVersion",
    "shared/Microsoft.AspNetCore.App/$runtimeVersion",
    "packs/Microsoft.NETCore.App.Ref/$runtimeVersion",
    "packs/Microsoft.AspNetCore.App.Ref/$runtimeVersion",
    "packs/Microsoft.NETCore.App.Host.win-x64/$runtimeVersion",
    'packs/NETStandard.Library.Ref/2.1.0'
)
foreach($relative in $directories){
    if(!(Test-Path -LiteralPath (Join-Path $sourceRoot $relative) -PathType Container)){throw "SDK構成が不足しています。 $relative"}
}
$rootFiles=@('dotnet.exe','LICENSE.txt','ThirdPartyNotices.txt')
foreach($name in $rootFiles){
    if(!(Test-Path -LiteralPath (Join-Path $sourceRoot $name) -PathType Leaf)){throw "SDK配布ファイルが不足しています。 $name"}
}
New-Item -ItemType Directory -Path $destinationPath|Out-Null
foreach($relative in $directories){
    $target=Join-Path $destinationPath $relative
    New-Item -ItemType Directory -Force (Split-Path $target)|Out-Null
    Copy-Item -LiteralPath (Join-Path $sourceRoot $relative) -Destination $target -Recurse
}
foreach($name in $rootFiles){Copy-Item -LiteralPath (Join-Path $sourceRoot $name) -Destination $destinationPath}
$files=@(Get-ChildItem -LiteralPath $destinationPath -File -Recurse|Sort-Object FullName|ForEach-Object {
    [ordered]@{path=[IO.Path]::GetRelativePath($destinationPath,$_.FullName).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}
})
$manifest=[ordered]@{format=1;runtimeIdentifier='win-x64';sdkVersion=$SdkVersion;runtimeVersion=$runtimeVersion;files=$files}
[IO.File]::WriteAllText((Join-Path $destinationPath 'bundled-sdk.json'),($manifest|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
[pscustomobject]@{Path=$destinationPath;SdkVersion=$SdkVersion;RuntimeVersion=$runtimeVersion;Files=$files.Count;Bytes=($files|ForEach-Object {$_.bytes}|Measure-Object -Sum).Sum}