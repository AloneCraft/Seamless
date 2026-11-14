[CmdletBinding()]
param([Parameter(Mandatory)][string]$Distribution,[ValidateSet('ServerWindows','ServerLinux')][string[]]$Targets=@('ServerWindows','ServerLinux'),[string]$WorkRoot='F:\Seamless\Management',[string]$LogRoot='F:\Seamless\Management',[switch]$VerifyWindowsConnection)
$ErrorActionPreference='Stop'
if($VerifyWindowsConnection -and ('ServerWindows' -notin $Targets)){
    throw '接続試験にはServerWindowsが必要です。'
}
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$id=[guid]::NewGuid().ToString('N')
if(![IO.Path]::IsPathFullyQualified($WorkRoot) -or ![IO.Path]::GetFullPath($WorkRoot).StartsWith('F:\Seamless\',[StringComparison]::OrdinalIgnoreCase) -or ![IO.Path]::IsPathFullyQualified($LogRoot) -or ![IO.Path]::GetFullPath($LogRoot).StartsWith('F:\Seamless\',[StringComparison]::OrdinalIgnoreCase)){throw 'WorkRootとLogRootはF:\Seamless配下の絶対パスを指定してください。'}
$work=Join-Path $WorkRoot ('package-test-'+$id)
$logs=Join-Path $LogRoot ('package-test-logs-'+$id)
New-Item -ItemType Directory -Path $work,$logs -Force | Out-Null
$env:TEMP=$work;$env:TMP=$work;$env:DOTNET_CLI_HOME=$work;$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false';$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
& (Join-Path $repository 'TestSeamless/Invoke-LocalProcess.ps1') -InitializeOnly | Out-Null
foreach($target in $Targets){
    $directory=Join-Path $Distribution $target
    $manifest=Get-Content -LiteralPath (Join-Path $directory 'distribution.json') -Raw | ConvertFrom-Json
    foreach($file in $manifest.Files){$path=Join-Path $directory $file.Path;if((Get-Item -LiteralPath $path).Length -ne $file.Bytes -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.Sha256){throw ('配布物の識別情報が一致しません: '+$path)}}
    $appHost=Get-Content -LiteralPath (Join-Path $directory 'ThirdParty/apphost.json') -Raw|ConvertFrom-Json
    if($appHost.Runtime -ne $manifest.Runtime -or !$appHost.PublishedAppHosts.Count -or $appHost.Notices.Count -ne 2){throw 'Apphostの由来と通知が配布先に対応していません。'}
    foreach($file in @($appHost.PublishedAppHosts)+@($appHost.Notices)){
        $prefix=[IO.Path]::GetFullPath($directory).TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar
        $path=[IO.Path]::GetFullPath((Join-Path $directory $file.Path))
        if(!$path.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.Sha256){throw 'Apphostまたは付随通知の識別情報が一致しません。'}
    }
}
if('ServerWindows' -notin $Targets){Write-Output '指定した配布物のSHA-256を照合しました。';Write-Output $logs;return;}
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
    if($VerifyWindowsConnection){
        $ready=Join-Path $work 'server-ready.json'
        $stop=Join-Path $work 'server-stop'
        $launchFile=Join-Path $work 'manager-launch.txt'
        $root=Join-Path $work 'local-instance'
        $owner=[Diagnostics.Process]::GetCurrentProcess()
        $server=$null
        $client=$null
        try{
            $start=[Diagnostics.ProcessStartInfo]::new($executable)
            $start.UseShellExecute=$false
            $start.CreateNoWindow=$true
            $start.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
            $start.WorkingDirectory=$work
            foreach($argument in @('serve','--config',(Join-Path $work 'local-launch.json'),'--port','0','--ready-file',$ready,
                '--stop-file',$stop,'--owner-pid',[string]$owner.Id,'--owner-start',[string]$owner.StartTime.ToUniversalTime().Ticks)){
                $start.ArgumentList.Add($argument)
            }
            $server=[Diagnostics.Process]::Start($start)
            $serverStart=$server.StartTime.ToUniversalTime()
            @{Pid=$server.Id;CreationUtc=$serverStart;Executable=$executable} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $logs 'server-process.json') -Encoding utf8
            $deadline=[DateTimeOffset]::UtcNow.AddSeconds(30)
            while(!(Test-Path -LiteralPath $ready)){
                if($server.HasExited -or [DateTimeOffset]::UtcNow -ge $deadline){throw '配布サーバーの起動を確認できませんでした。'}
                Start-Sleep -Milliseconds 100
            }
            $identity=Get-Content -LiteralPath $ready -Raw | ConvertFrom-Json
            if($identity.pid -ne $server.Id -or ([datetime]$identity.creationUtc).ToUniversalTime() -ne $serverStart){throw '配布サーバーの起動記録が一致しません。'}
            $connection=Get-Content -LiteralPath (Join-Path $root 'manager-connection.json') -Raw | ConvertFrom-Json
            if($connection.instanceId -ne $identity.instanceId -or $connection.pid -ne $server.Id){throw 'Managerの所有者IPC記録が一致しません。'}
            $result=& (Join-Path $repository 'TestSeamless/Invoke-LocalProcess.ps1') -Executable $executable -Arguments @('manager','--root',$root,'--launch-file',$launchFile) -WorkingDirectory $work -ArtifactDirectory (Join-Path $logs 'manager-launch') -TimeoutSeconds 30
            if($result.Outcome -ne 'Passed'){throw '所有者IPCからManagerを起動できませんでした。'}
            $launch=[Uri](Get-Content -LiteralPath $launchFile -Raw)
            $origin=[Uri]$identity.managerOrigin
            $base=[Uri]::new($origin,[string]$identity.managerPath)
            if($launch.GetLeftPart([UriPartial]::Authority) -ne $origin.GetLeftPart([UriPartial]::Authority) -or $launch.AbsolutePath -ne $base.AbsolutePath+'launch'){throw '起動URLが開始したWebホストと一致しません。'}
            $handler=[Net.Http.HttpClientHandler]::new()
            $handler.AllowAutoRedirect=$false
            $client=[Net.Http.HttpClient]::new($handler)
            $client.Timeout=[TimeSpan]::FromSeconds(15)
            $unauthenticated=$client.GetAsync($base).GetAwaiter().GetResult()
            if([int]$unauthenticated.StatusCode -ne 401){throw '未認証のManager要求が拒否されませんでした。'}
            $request=[Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Post,[Uri]::new($base,'session'))
            $request.Headers.Add('Origin',$origin.GetLeftPart([UriPartial]::Authority))
            $request.Content=[Net.Http.StringContent]::new($launch.Fragment.Substring(1),[Text.Encoding]::UTF8,'text/plain')
            $response=$client.SendAsync($request).GetAwaiter().GetResult()
            $request.Dispose()
            Remove-Item -LiteralPath $launchFile
            if([int]$response.StatusCode -ne 204){throw '所有者の起動情報をセッションへ交換できませんでした。'}
            $page=$client.GetAsync($base).GetAwaiter().GetResult()
            if([int]$page.StatusCode -ne 200 -or !$page.Content.ReadAsStringAsync().GetAwaiter().GetResult().Contains('Seamless')){throw '認証後のManager画面を取得できませんでした。'}
            foreach($asset in @('_content/Seamless.Management.Components/manager.css','_framework/blazor.web.js')){
                $assetResponse=$client.GetAsync([Uri]::new($base,$asset)).GetAwaiter().GetResult()
                if([int]$assetResponse.StatusCode -ne 200){throw ('Manager資産を取得できませんでした。 '+$asset)}
                $assetResponse.Dispose()
            }
            @{Pid=$server.Id;InstanceId=$identity.instanceId;Unauthenticated=[int]$unauthenticated.StatusCode;Authenticated=[int]$page.StatusCode;OwnerLaunch=$true;Assets=$true} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $logs 'manager-web-http.json') -Encoding utf8
            $unauthenticated.Dispose()
            $response.Dispose()
            $page.Dispose()
        }finally{
            if($client){$client.Dispose()}
            if($server -and !$server.HasExited){
                New-Item -ItemType File -Path $stop -Force | Out-Null
                if(!$server.WaitForExit(15000)){throw '配布サーバーの正常停止を確認できませんでした。'}
            }
            if(Test-Path -LiteralPath $launchFile){Remove-Item -LiteralPath $launchFile}
        }
        if($server.ExitCode -ne 0 -or (Test-Path -LiteralPath (Join-Path $root 'manager-connection.json')) -or (Test-Path -LiteralPath (Join-Path $root 'local-connection.json'))){throw '配布サーバーの終了状態または接続情報ファイルを確認してください。'}
        $server.Dispose()
        Write-Output '配布サーバーの所有者IPC、ブラウザー認証と画面・資産のHTTP取得、正常停止を確認しました。'
    }
}finally{Remove-Item Env:SEAMLESS_MANAGEMENT_PASSWORD,Env:SEAMLESS_MANAGEMENT_ROOT,Env:SEAMLESS_MANAGEMENT_CERTIFICATE -ErrorAction SilentlyContinue}
Write-Output '配布物のSHA-256照合、Windows CLIのhelp/init/backup/restore/reset-password/setup-local、環境変数からの設定取得を確認しました。'
Write-Output $logs
