#requires -Version 7.0
param(
    [Parameter(Mandatory=$true)][string]$Name,
    [Parameter(Mandatory=$true)][string[]]$Arguments,
    [ValidateSet('SSD','HDD')][string]$Storage='SSD',
    [int]$TimeoutSeconds=1800
)
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
& (Join-Path $root 'Invoke-StorageTest.ps1') -Name $Name -Arguments $Arguments -Storage $Storage -TimeoutSeconds $TimeoutSeconds
exit $LASTEXITCODE
