[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')][string]$Configuration='Release',
    [string]$DataRoot='D:/CodexTemp/Seamless/RoslynRoundtrip',
    [string]$CacheRoot='F:/CodexTemp/Seamless/RoslynRoundtrip'
)
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
foreach($path in @($DataRoot,$CacheRoot)){
    if(-not [IO.Path]::IsPathFullyQualified($path) -or [IO.Path]::GetPathRoot([IO.Path]::GetFullPath($path)) -eq 'C:\'){throw 'Use absolute non-C output directories.'}
}
& "$repository/TestSeamless/Initialize-Storage.ps1" -ArchiveRoot $CacheRoot -TemporaryRoot (Join-Path $DataRoot 'tmp')|Out-Null
$tool=Join-Path $DataRoot "bin/Seamless/Database/Tool/$Configuration/net10.0/Server.dll"
if(-not (Test-Path -LiteralPath $tool)){throw 'Build the Tool suite first.'}
$id=[guid]::NewGuid().ToString('N')
$output=Join-Path $DataRoot "cli-$id";$logs=Join-Path $CacheRoot "cli-$id"
[IO.Directory]::CreateDirectory($output)|Out-Null
[IO.File]::WriteAllText((Join-Path $output 'owner.json'),(@{Owner=$id;Kind='ConversionCli';CreatedUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json))
function Invoke-Conversion([string]$Name,[string[]]$Arguments,[int]$ExpectedExit=0){
    $artifact=Join-Path $logs $Name
    $result=& "$repository/TestSeamless/Invoke-LocalProcess.ps1" -Executable (Get-Command dotnet).Source -Arguments (@($tool)+$Arguments) -WorkingDirectory $DataRoot -ArtifactDirectory $artifact -TimeoutSeconds 120
    Get-Content -LiteralPath (Join-Path $artifact 'stdout.log')
    if($result.Task.exitCode -ne $ExpectedExit){Get-Content -LiteralPath (Join-Path $artifact 'stderr.log');throw "Unexpected conversion result: $artifact"}
}
foreach($vendor in @('sqlserver','mysql')){
    $sql='CREATE TABLE Items (Id int NOT NULL,Name varchar(20) NULL,Amount bigint NOT NULL,PRIMARY KEY(Id));'
    $input=Join-Path $output "$vendor.sql";[IO.File]::WriteAllText($input,$sql)
    $import=Join-Path $output "$vendor-csharp"
    Invoke-Conversion "$vendor-to-csharp" @('database','sql','convert','--sql',$input,'--name','CliShop','--vendor',$vendor,'--output',$import)
    $manifest=Get-Content -LiteralPath (Join-Path $import 'manifest.json') -Raw|ConvertFrom-Json
    if($manifest.Status -ne 'Converted'){throw 'SQL conversion did not complete.'}
    foreach($target in @('sqlserver','mysql')){
        $export=Join-Path $output "$vendor-to-$target"
        Invoke-Conversion "$vendor-to-$target" @('database','csharp','export','--snapshot',(Join-Path $import 'snapshot.json'),'--type',$manifest.TypeName,'--vendor',$target,'--output',$export)
        $converted=Get-Content -LiteralPath (Join-Path $export 'manifest.json') -Raw|ConvertFrom-Json
        if($converted.Status -ne 'Converted' -or $converted.Tables -ne 1){throw 'C# conversion did not complete.'}
    }
}
$unsupported=Join-Path $output 'unsupported.sql';[IO.File]::WriteAllText($unsupported,'CREATE VIEW ItemsView AS SELECT 1 AS Id;')
Invoke-Conversion 'unsupported' @('database','sql','convert','--sql',$unsupported,'--name','Unsupported','--vendor','sqlserver','--output',(Join-Path $output 'partial')) 3
$partial=Get-Content -LiteralPath (Join-Path $output 'partial/manifest.json') -Raw|ConvertFrom-Json
if($partial.Status -ne 'Partial'){throw 'Unsupported SQL was incorrectly marked complete.'}
Write-Output "Conversion CLI passed; output=$output; logs=$logs"
