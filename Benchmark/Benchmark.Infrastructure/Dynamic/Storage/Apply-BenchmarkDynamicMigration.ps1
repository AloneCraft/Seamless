[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$DatabasePath,

    [Parameter(Mandatory=$true)]
    [string]$MigrationPath,

    [Parameter(Mandatory=$true)]
    [ValidateRange(1,[int]::MaxValue)]
    [int]$TargetVersion,

    [string]$SqliteExecutable="sqlite3"
)

Set-StrictMode -Version Latest
$ErrorActionPreference="Stop"

function Invoke-SqliteText{
    param(
        [Parameter(Mandatory=$true)]
        [string]$Executable,

        [Parameter(Mandatory=$true)]
        [string]$ResolvedDatabasePath,

        [Parameter(Mandatory=$true)]
        [string]$Sql
    )

    $startInfo=[System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName=$Executable
    $startInfo.UseShellExecute=$false
    $startInfo.CreateNoWindow=$true
    $startInfo.RedirectStandardInput=$true
    $startInfo.RedirectStandardOutput=$true
    $startInfo.RedirectStandardError=$true
    $startInfo.StandardInputEncoding=[System.Text.UTF8Encoding]::new($false)
    $startInfo.StandardOutputEncoding=[System.Text.UTF8Encoding]::new($false)
    $startInfo.StandardErrorEncoding=[System.Text.UTF8Encoding]::new($false)
    $startInfo.ArgumentList.Add($ResolvedDatabasePath)

    $process=[System.Diagnostics.Process]::new()
    $process.StartInfo=$startInfo
    try{
        if(-not $process.Start()){
            throw "sqlite3 process did not start."
        }
        $process.StandardInput.Write($Sql)
        $process.StandardInput.Close()
        $standardOutput=$process.StandardOutput.ReadToEnd()
        $standardError=$process.StandardError.ReadToEnd()
        $process.WaitForExit()
        if($process.ExitCode-ne 0){
            throw "sqlite3 failed with exit code $($process.ExitCode): $standardError"
        }
        return $standardOutput.Trim()
    }finally{
        $process.Dispose()
    }
}

$resolvedDatabasePath=(Resolve-Path -LiteralPath $DatabasePath).Path
$resolvedMigrationPath=(Resolve-Path -LiteralPath $MigrationPath).Path
$migrationSql=Get-Content -LiteralPath $resolvedMigrationPath -Raw -Encoding UTF8

if($migrationSql-match "(?im)^\s*(BEGIN|COMMIT|ROLLBACK)\b"){
    throw "Migration SQL must not contain transaction control statements; this script owns the transaction."
}

$timestamp=[DateTimeOffset]::UtcNow.ToString("yyyyMMddTHHmmssfffZ")
$databaseDirectory=Split-Path -Parent $resolvedDatabasePath
$databaseName=Split-Path -Leaf $resolvedDatabasePath
$backupDirectory=Join-Path $databaseDirectory "$databaseName.backup-$timestamp"
$null=New-Item -ItemType Directory -Path $backupDirectory

$backupEntries=[System.Collections.Generic.List[object]]::new()
$databaseArtifacts=@(
    $resolvedDatabasePath,
    "$resolvedDatabasePath-wal",
    "$resolvedDatabasePath-shm"
)

foreach($artifactPath in $databaseArtifacts){
    $exists=Test-Path -LiteralPath $artifactPath -PathType Leaf
    $backupPath=$null
    $sha256=$null
    if($exists){
        $backupPath=Join-Path $backupDirectory (Split-Path -Leaf $artifactPath)
        Copy-Item -LiteralPath $artifactPath -Destination $backupPath
        $sha256=(Get-FileHash -LiteralPath $backupPath -Algorithm SHA256).Hash
    }
    $backupEntries.Add([pscustomobject]@{
        SourcePath=$artifactPath
        Existed=$exists
        BackupPath=$backupPath
        SHA256=$sha256
    })
}

$currentVersionText=Invoke-SqliteText `
    -Executable $SqliteExecutable `
    -ResolvedDatabasePath $resolvedDatabasePath `
    -Sql "PRAGMA user_version;"
$currentVersion=[int]$currentVersionText
if($currentVersion-ge $TargetVersion){
    throw "TargetVersion must be greater than the current version. Current=$currentVersion Target=$TargetVersion Backup=$backupDirectory"
}

$manifest=[pscustomobject]@{
    DatabasePath=$resolvedDatabasePath
    MigrationPath=$resolvedMigrationPath
    CurrentVersion=$currentVersion
    TargetVersion=$TargetVersion
    CreatedAt=[DateTimeOffset]::UtcNow.ToString("O")
    Files=$backupEntries
}
$manifestPath=Join-Path $backupDirectory "backup-manifest.json"
$manifest |
    ConvertTo-Json -Depth 5 |
    Set-Content -LiteralPath $manifestPath -Encoding UTF8

$transactionSql=@"
PRAGMA foreign_keys=ON;
PRAGMA busy_timeout=30000;
BEGIN IMMEDIATE;
$migrationSql
PRAGMA user_version=$TargetVersion;
COMMIT;
"@

try{
    $null=Invoke-SqliteText `
        -Executable $SqliteExecutable `
        -ResolvedDatabasePath $resolvedDatabasePath `
        -Sql $transactionSql
    $actualVersionText=Invoke-SqliteText `
        -Executable $SqliteExecutable `
        -ResolvedDatabasePath $resolvedDatabasePath `
        -Sql "PRAGMA user_version;"
    $actualVersion=[int]$actualVersionText
    if($actualVersion-ne $TargetVersion){
        throw "Migration did not set the requested version. Expected=$TargetVersion Actual=$actualVersion"
    }
}catch{
    throw "Migration failed. The database was not automatically restored. Backup=$backupDirectory Error=$($_.Exception.Message)"
}

[pscustomobject]@{
    DatabasePath=$resolvedDatabasePath
    PreviousVersion=$currentVersion
    CurrentVersion=$TargetVersion
    BackupDirectory=$backupDirectory
    ManifestPath=$manifestPath
}
