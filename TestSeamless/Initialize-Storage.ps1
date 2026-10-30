param([string]$ArchiveRoot,[string]$TemporaryRoot)
$ErrorActionPreference='Stop'
if(-not $ArchiveRoot){$ArchiveRoot=$env:SEAMLESS_COMPARISON_ARCHIVE_ROOT}
if(-not $ArchiveRoot){if(Test-Path -LiteralPath 'F:\'){$ArchiveRoot='F:\Seamless\Comparison'}else{$ArchiveRoot=Join-Path (Split-Path $PSScriptRoot -Parent) 'Work\TestSeamless'}}
if(-not $TemporaryRoot){$TemporaryRoot=$env:SEAMLESS_COMPARISON_TEMP_ROOT}
if(-not $TemporaryRoot){if(Test-Path -LiteralPath 'D:\'){$TemporaryRoot='D:\Seamless\Comparison\Temp'}else{$TemporaryRoot=Join-Path $ArchiveRoot 'Temp'}}
$ArchiveRoot=[IO.Path]::GetFullPath($ArchiveRoot);$TemporaryRoot=[IO.Path]::GetFullPath($TemporaryRoot)
foreach($path in @($ArchiveRoot,$TemporaryRoot)){if([IO.Path]::GetPathRoot($path) -eq 'C:\'){throw 'Comparison output must not use the C drive.'};New-Item -ItemType Directory -Force -Path $path|Out-Null}
$env:SEAMLESS_COMPARISON_ARCHIVE_ROOT=$ArchiveRoot;$env:SEAMLESS_COMPARISON_TEMP_ROOT=$TemporaryRoot
$env:SEAMLESS_COMPARISON_PACKAGE_ROOT=Join-Path $ArchiveRoot 'packages'
$env:TEMP=$TemporaryRoot;$env:TMP=$TemporaryRoot
$env:NUGET_PACKAGES=Join-Path $ArchiveRoot 'product-packages';$env:NUGET_HTTP_CACHE_PATH=Join-Path $ArchiveRoot 'nuget-http-cache';$env:NUGET_SCRATCH=Join-Path $TemporaryRoot 'nuget-scratch'
$env:DOTNET_CLI_HOME=Join-Path $ArchiveRoot 'dotnet-home';$env:DOTNET_BUNDLE_EXTRACT_BASE_DIR=Join-Path $TemporaryRoot 'bundles'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false';$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1';$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
[pscustomobject]@{ArchiveRoot=$ArchiveRoot;TemporaryRoot=$TemporaryRoot;PackageRoot=$env:SEAMLESS_COMPARISON_PACKAGE_ROOT}
