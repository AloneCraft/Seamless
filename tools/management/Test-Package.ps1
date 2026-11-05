[CmdletBinding()]
param([Parameter(Mandatory)][string]$Distribution)
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$id=[guid]::NewGuid().ToString('N')
$work=Join-Path 'D:\CodexTemp\SeamlessManagement-20261102' ('package-test-'+$id)
$logs=Join-Path 'F:\CodexTemp\SeamlessManagement-20261102' ('package-test-'+$id)
New-Item -ItemType Directory -Path $work,$logs -Force | Out-Null
$env:TEMP=$work;$env:TMP=$work;$env:DOTNET_CLI_HOME='F:\CodexTemp\SeamlessManagement-20261102'
. (Join-Path $repository 'TestSeamless/Invoke-LocalProcess.ps1') -InitializeOnly | Out-Null
foreach($target in @('ServerWindows','ServerLinux','WindowsUI')){
    $directory=Join-Path $Distribution $target
    $manifest=Get-Content -LiteralPath (Join-Path $directory 'distribution.json') -Raw | ConvertFrom-Json
    foreach($file in $manifest.Files){$path=Join-Path $directory $file.Path;if((Get-Item -LiteralPath $path).Length -ne $file.Bytes -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.Sha256){throw ('配布物の識別情報が一致しません: '+$path)}}
}
$executable=Join-Path $Distribution 'ServerWindows/Seamless.Management.Server.exe'
$env:SEAMLESS_MANAGEMENT_PASSWORD='PackageTestPassword-123'
try{
    $steps=@(
        @{Name='help';Arguments=@('--help')},
        @{Name='init';Arguments=@('init','--root',(Join-Path $work 'instance'),'--admin','admin')},
        @{Name='backup';Arguments=@('backup','--root',(Join-Path $work 'instance'),'--destination',(Join-Path $work 'backup'))},
        @{Name='restore';Arguments=@('restore','--backup',(Join-Path $work 'backup'),'--destination',(Join-Path $work 'restored'))},
        @{Name='reset-password';Arguments=@('reset-password','--root',(Join-Path $work 'restored'),'--user','admin','--backup',(Join-Path $work 'before-reset'))},
        @{Name='setup-local';Arguments=@('setup-local','--root',(Join-Path $work 'local-instance'),'--admin','admin','--config',(Join-Path $work 'local-launch.json'))}
    )
    foreach($step in $steps){$result=& (Join-Path $repository 'TestSeamless/Invoke-LocalProcess.ps1') -Executable $executable -Arguments $step.Arguments -WorkingDirectory $work -ArtifactDirectory (Join-Path $logs $step.Name) -TimeoutSeconds 60;if($result.Outcome -ne 'Passed'){throw ('配布CLI試験が失敗しました: '+$step.Name)}}
    $env:SEAMLESS_MANAGEMENT_ROOT=Join-Path $work 'restored'
    $env:SEAMLESS_MANAGEMENT_CERTIFICATE=Join-Path $work 'missing-certificate.pfx'
    $result=& (Join-Path $repository 'TestSeamless/Invoke-LocalProcess.ps1') -Executable $executable -Arguments @('serve','--port','0') -WorkingDirectory $work -ArtifactDirectory (Join-Path $logs 'environment') -TimeoutSeconds 60
    $errorText=Get-Content -LiteralPath (Join-Path $logs 'environment/stderr.log') -Raw
    if($result.Outcome -eq 'Passed' -or [string]::IsNullOrWhiteSpace($errorText) -or $errorText.Contains('SEAMLESS_MANAGEMENT_ROOT') -or $errorText.Contains('SEAMLESS_MANAGEMENT_CERTIFICATE') -or $errorText.Contains('初期化')){throw '環境変数による保存先・証明書の解決を確認できませんでした。'}
}finally{Remove-Item Env:SEAMLESS_MANAGEMENT_PASSWORD,Env:SEAMLESS_MANAGEMENT_ROOT,Env:SEAMLESS_MANAGEMENT_CERTIFICATE -ErrorAction SilentlyContinue}
Write-Output '配布物のSHA-256照合、Windows CLIのhelp/init/backup/restore/reset-password/setup-local、環境変数からの設定取得を確認しました。'
Write-Output $logs
