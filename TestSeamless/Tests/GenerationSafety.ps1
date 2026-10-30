param([string]$ArtifactRoot,[string]$SourceScript)
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$source=Join-Path $PSScriptRoot '..\Invoke-Comparison.ps1'
if($SourceScript){$source=[IO.Path]::GetFullPath($SourceScript)}
if(-not $ArtifactRoot){$ArtifactRoot=Join-Path $repo 'Work\TestSeamless\Implementation\generation-safety-fixture'}
$ArtifactRoot=[IO.Path]::GetFullPath($ArtifactRoot)
if(-not (Test-Path -LiteralPath $ArtifactRoot)){New-Item -ItemType Directory -Force -Path $ArtifactRoot|Out-Null}
$ArtifactRoot=Join-Path $ArtifactRoot ('generation-safety-'+[DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')+'-'+[Guid]::NewGuid().ToString('N'))
$ManagedRoot=Join-Path $ArtifactRoot 'managed'
$utf8=New-Object Text.UTF8Encoding $false
$results=New-Object System.Collections.Generic.List[object]
function Add-Result([string]$name,[string]$status,[string]$detail){$results.Add([pscustomobject]@{name=$name;status=$status;detail=$detail})}
function Assert([bool]$condition,[string]$message){if(-not $condition){throw $message}}
function New-Tree([string]$path){$resolved=[IO.Path]::GetFullPath($path);$prefix=$ArtifactRoot.TrimEnd('\')+'\';if(-not $resolved.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Fixture deletion path escapes the artifact root.'};if(Test-Path -LiteralPath $resolved){Remove-Item -LiteralPath $resolved -Recurse -Force};New-Item -ItemType Directory -Force -Path $path|Out-Null}
function Write-JsonFixture([string]$path,$value){[IO.File]::WriteAllText($path,(ConvertTo-Json -InputObject $value -Depth 30),$utf8)}
function New-Generation([string]$id,[string[]]$databaseNames){$path=Join-Path $ManagedRoot ('generations\'+$id);New-Item -ItemType Directory -Force -Path $path|Out-Null;[IO.File]::WriteAllText((Join-Path $path '.seamless-generation-owner'),$id);Write-JsonFixture (Join-Path $path 'generation.json') @{Id=$id;State='Validated';SourceDatabaseNames=@($databaseNames);Databases=@($databaseNames|ForEach-Object {@{Name=$_}})};Write-JsonFixture (Join-Path $path 'validation.json') @{Generation=$id;Status='Passed';SourceInventoryVerified=$true};return $path}
function Set-Ledger($entries){Write-JsonFixture (Join-Path $ManagedRoot 'managed-generations.json') @($entries)}
function Run-Case([string]$name,[scriptblock]$test){$before=$results.Count;try{& $test;if(-not ($results.Count -gt $before -and $results[$results.Count-1].status -eq 'Unavailable')){Add-Result $name 'Passed' 'assertions satisfied'}}catch{Add-Result $name 'Failed' $_.Exception.Message}}
try{
    New-Item -ItemType Directory -Force -Path $ArtifactRoot|Out-Null
    New-Tree $ManagedRoot
    $astTokens=$null;$astErrors=$null;$ast=[System.Management.Automation.Language.Parser]::ParseFile($source,[ref]$astTokens,[ref]$astErrors)
    if($astErrors.Count){throw ('Source parse failed: '+($astErrors|ForEach-Object Message -join '; '))}
    $wanted=@('Write-Json','Resolve-Managed','Register-Generation','Remove-RetiredGenerations','Stop-OwnedServer','Get-ValidatedGeneration')
    $found=@($ast.FindAll({param($node)$node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $wanted -contains $node.Name},$true))
    foreach($name in $wanted){$matches=@($found|Where-Object Name -eq $name);if($matches.Count -ne 1){throw "Expected one source function '$name'; found $($matches.Count)."};Invoke-Expression $matches[0].Extent.Text}
    Add-Result 'source-ast-extraction' 'Passed' 'Only selected FunctionDefinitionAst bodies were loaded; script main was not executed.'

    Run-Case 'activation-rejects-invalid-id-and-owner' {
        New-Tree $ManagedRoot;Set-Ledger @()
        foreach($id in @('missing','../escape','', 'a/b')){$threw=$false;try{[void](Get-ValidatedGeneration $id)}catch{$threw=$true};Assert $threw "Invalid ID was accepted: $id"}
        $path=New-Generation 'current' @('KeepDb');Set-Ledger @(@{Id='current';RelativePath='generations\current'});[IO.File]::WriteAllText((Join-Path $path '.seamless-generation-owner'),'other');$threw=$false;try{[void](Get-ValidatedGeneration 'current')}catch{$threw=$true};Assert $threw 'Owner mismatch accepted'
    }
    Run-Case 'activation-proofs-and-tampering' {
        New-Tree $ManagedRoot;$path=New-Generation 'current' @('KeepDb');Set-Ledger @(@{Id='current';RelativePath='generations\current'})
        $script:ArchiveRoot=Join-Path $ArtifactRoot 'archive';$script:packageRoot=Join-Path $ArchiveRoot 'packages';$script:Configuration='Release'
        $version='v1';$feed=Join-Path $ArchiveRoot 'feeds\v1';$package=Join-Path $packageRoot 'v1\seamless.test\v1'
        $generated=Join-Path $path 'build\obj\TestSeamless.Queries\generated\Release';$bin=Join-Path $path 'build\bin\TestSeamless\Release\net10.0'
        foreach($folder in @($package,$feed,$generated,$bin,(Join-Path $path 'build\obj\TestSeamless'),(Join-Path $path 'schema'),(Join-Path $path 'modules'))){New-Item -ItemType Directory -Path $folder -Force|Out-Null}
        [IO.File]::WriteAllText((Join-Path $generated 'test.cs'),'interceptor');[IO.File]::WriteAllText((Join-Path $path 'schema\schema.dll'),'schema');[IO.File]::WriteAllText((Join-Path $path 'modules\module.dll'),'module');[IO.File]::WriteAllText((Join-Path $bin 'TestSeamless.dll'),'host')
        $fhash=(Get-FileHash (Join-Path $generated 'test.cs') -Algorithm SHA256).Hash;$mhash=(Get-FileHash (Join-Path $path 'modules\module.dll') -Algorithm SHA256).Hash
        Write-JsonFixture (Join-Path $path 'generation.json') @{Id='current';State='Validated';ProductVersion=$version;SourceDatabaseNames=@('KeepDb');Databases=@(@{Name='KeepDb';Fingerprint='fp'});ModuleManifest='modules\native-module.json';SchemaAssembly='schema\schema.dll'}
        Write-JsonFixture (Join-Path $path 'build\obj\TestSeamless\project.assets.json') @{libraries=@{'Seamless.Test/v1'=@{type='package';path='seamless.test/v1'}}}
        Write-JsonFixture (Join-Path $package '.nupkg.metadata') @{source=$feed;contentHash='hash'}
        Write-JsonFixture (Join-Path $path 'package-provenance.json') @(@{Package='Seamless.Test/v1';Path=$package;Feed=$feed;ContentHash='hash'})
        Write-JsonFixture (Join-Path $path 'flinq-proof.json') @{PackageVersion=$version;Interceptors=@(@{Path=(Join-Path $generated 'test.cs');Sha256=$fhash})}
        Write-JsonFixture (Join-Path $path 'modules\native-module.json') @{ModuleId='current';ProductVersion=$version;AssemblyPath='module.dll';AssemblySha256=$mhash;Queries=@(@{Database='KeepDb';SchemaFingerprint='fp'})}
        $candidate=Get-ValidatedGeneration 'current';Assert ($candidate.Id -eq 'current') 'Valid evidence was rejected'
        foreach($kind in @('validation','package','flinq','module')){
            switch($kind){
                'validation' {$file=Join-Path $path 'validation.json';$changed=@{Generation='current';Status='Passed';SourceInventoryVerified=$false}}
                'package' {$file=Join-Path $package '.nupkg.metadata';$changed=@{source=$feed;contentHash='wrong'}}
                'flinq' {$file=Join-Path $path 'flinq-proof.json';$changed=@{PackageVersion=$version;Interceptors=@(@{Path=(Join-Path $generated 'test.cs');Sha256='bad'})}}
                'module' {$file=Join-Path $path 'modules\native-module.json';$changed=@{ModuleId='current';ProductVersion=$version;AssemblyPath='module.dll';AssemblySha256='bad';Queries=@()}}
            }
            $before=[IO.File]::ReadAllText($file);Write-JsonFixture $file $changed;$threw=$false
            try{[void](Get-ValidatedGeneration 'current')}catch{$threw=$true}finally{[IO.File]::WriteAllText($file,$before,$utf8)}
            Assert $threw "Tampered $kind evidence was accepted"
        }
    }
    Run-Case 'registration-and-duplicate' {
        New-Tree $ManagedRoot;Register-Generation 'fresh-a';$ledger=@(Get-Content -Raw (Join-Path $ManagedRoot 'managed-generations.json')|ConvertFrom-Json);Assert ($ledger.Count -eq 1 -and $ledger[0].Id -eq 'fresh-a' -and $ledger[0].RelativePath -eq 'generations\fresh-a') 'registration entry differs';$threw=$false;try{Register-Generation 'fresh-a'}catch{$threw=$true};Assert $threw 'duplicate generation registration was accepted'
    }
    Run-Case 'remove-only-retired-after-db-removal' {
        New-Tree $ManagedRoot;$current=New-Generation 'current' @('KeepDb');$old=New-Generation 'retired' @('KeepDb','RemovedDb');$manual=New-Generation 'manual' @('KeepDb');$entries=@(@{Id='current';RelativePath='generations\current'},@{Id='retired';RelativePath='generations\retired'});Set-Ledger $entries;Remove-RetiredGenerations $current;Assert (Test-Path -LiteralPath $current) 'current generation was deleted';Assert (-not (Test-Path -LiteralPath $old)) 'retired generation containing removed DB was retained';Assert (Test-Path -LiteralPath $manual) 'unregistered hand-written generation was deleted';$ledger=@(Get-Content -Raw (Join-Path $ManagedRoot 'managed-generations.json')|ConvertFrom-Json);Assert ($ledger.Count -eq 1 -and $ledger[0].Id -eq 'current') 'ledger did not retain only current generation'
    }
    Run-Case 'retain-old-generation-when-no-db-removed' {
        New-Tree $ManagedRoot;$current=New-Generation 'current' @('KeepDb');$old=New-Generation 'old' @('KeepDb');Set-Ledger @(@{Id='current';RelativePath='generations\current'},@{Id='old';RelativePath='generations\old'});Remove-RetiredGenerations $current;Assert (Test-Path -LiteralPath $old) 'old generation without removed databases was deleted';$ledger=@(Get-Content -Raw (Join-Path $ManagedRoot 'managed-generations.json')|ConvertFrom-Json);Assert ($ledger.Count -eq 2) 'retained generation was dropped from ledger'
    }
    Run-Case 'selected-subset-does-not-mean-source-deletion' {
        New-Tree $ManagedRoot;$current=New-Generation 'current' @('KeepDb');$old=New-Generation 'old' @('KeepDb','UnselectedDb');$manifest=Get-Content -Raw (Join-Path $current 'generation.json')|ConvertFrom-Json;$manifest.SourceDatabaseNames=@('KeepDb','UnselectedDb');Write-JsonFixture (Join-Path $current 'generation.json') $manifest;Set-Ledger @(@{Id='current';RelativePath='generations\current'},@{Id='old';RelativePath='generations\old'});Remove-RetiredGenerations $current;Assert (Test-Path -LiteralPath $old) 'Unselected but existing source DB caused old artifacts to be deleted'
    }
    Run-Case 'missing-inventory-or-final-proof-refuses-cleanup' {
        foreach($missing in @('inventory','proof')){
            New-Tree $ManagedRoot;$current=New-Generation 'current' @('KeepDb');$old=New-Generation 'old' @('RemovedDb');Set-Ledger @(@{Id='current';RelativePath='generations\current'},@{Id='old';RelativePath='generations\old'});$manifest=Get-Content -Raw (Join-Path $current 'generation.json')|ConvertFrom-Json
            if($missing -eq 'inventory'){$manifest.PSObject.Properties.Remove('SourceDatabaseNames');Write-JsonFixture (Join-Path $current 'generation.json') $manifest}else{Write-JsonFixture (Join-Path $current 'validation.json') @{Generation='current';Status='Passed'}}
            $before=[IO.File]::ReadAllText((Join-Path $ManagedRoot 'managed-generations.json'));$threw=$false;try{Remove-RetiredGenerations $current}catch{$threw=$true};Assert $threw 'Incomplete source deletion evidence was accepted';Assert (Test-Path -LiteralPath $old) 'Cleanup without full verified inventory deleted data';Assert ([IO.File]::ReadAllText((Join-Path $ManagedRoot 'managed-generations.json')) -eq $before) 'Refused cleanup changed ledger'
        }
    }
    Run-Case 'owner-marker-mismatch-refuses-cleanup' {
        New-Tree $ManagedRoot;$current=New-Generation 'current' @('KeepDb');$bad=New-Generation 'bad' @('RemovedDb');[IO.File]::WriteAllText((Join-Path $bad '.seamless-generation-owner'),'not-bad');Set-Ledger @(@{Id='current';RelativePath='generations\current'},@{Id='bad';RelativePath='generations\bad'});$before=[IO.File]::ReadAllText((Join-Path $ManagedRoot 'managed-generations.json'));$threw=$false;try{Remove-RetiredGenerations $current}catch{$threw=$true};Assert $threw 'owner mismatch did not refuse';Assert (Test-Path -LiteralPath $bad) 'owner mismatch cleanup deleted directory';Assert ([IO.File]::ReadAllText((Join-Path $ManagedRoot 'managed-generations.json')) -eq $before) 'ledger changed despite refusal'
    }
    Run-Case 'absolute-and-traversal-paths-rejected' {
        New-Tree $ManagedRoot;$abs=$false;$trav=$false;try{Resolve-Managed ([IO.Path]::GetFullPath((Join-Path $ArtifactRoot 'outside')))}catch{$abs=$true};try{Resolve-Managed '..\outside'}catch{$trav=$true};Assert $abs 'absolute path was accepted';Assert $trav 'traversal path was accepted'
    }
    Run-Case 'reparse-point-refuses-cleanup' {
        New-Tree $ManagedRoot;$current=New-Generation 'current' @('KeepDb');$old=New-Generation 'linked' @('RemovedDb');$target=Join-Path $ManagedRoot 'outside-target';New-Item -ItemType Directory -Path $target|Out-Null
        try{New-Item -ItemType Junction -Path (Join-Path $old 'junction') -Target $target -ErrorAction Stop|Out-Null}catch{Add-Result 'reparse-point-refuses-cleanup' 'Unavailable' ('Junction creation unavailable: '+$_.Exception.Message);return}
        Set-Ledger @(@{Id='current';RelativePath='generations\current'},@{Id='linked';RelativePath='generations\linked'});$threw=$false;try{Remove-RetiredGenerations $current}catch{$threw=$true};Assert $threw 'reparse point did not refuse cleanup';Assert (Test-Path -LiteralPath $old) 'directory with reparse point was deleted'
    }
    Run-Case 'ancestor-reparse-point-refuses-cleanup' {
        New-Tree $ManagedRoot;$redirect=Join-Path $ArtifactRoot 'redirected-generations';New-Item -ItemType Directory -Path $redirect|Out-Null
        try{New-Item -ItemType Junction -Path (Join-Path $ManagedRoot 'generations') -Target $redirect -ErrorAction Stop|Out-Null}catch{Add-Result 'ancestor-reparse-point-refuses-cleanup' 'Unavailable' ('Junction creation unavailable: '+$_.Exception.Message);return}
        $current=New-Generation 'current' @('KeepDb');$old=New-Generation 'old' @('RemovedDb');Set-Ledger @(@{Id='current';RelativePath='generations\current'},@{Id='old';RelativePath='generations\old'});$threw=$false;try{Remove-RetiredGenerations $current}catch{$threw=$true};Assert $threw 'ancestor junction did not refuse cleanup';Assert (Test-Path -LiteralPath (Join-Path $redirect 'old')) 'directory beyond ancestor junction was deleted'
    }
    Run-Case 'rerun-is-idempotent' {
        New-Tree $ManagedRoot;$current=New-Generation 'current' @('KeepDb');$old=New-Generation 'old' @('KeepDb');Set-Ledger @(@{Id='current';RelativePath='generations\current'},@{Id='old';RelativePath='generations\old'});Remove-RetiredGenerations $current;$first=[IO.File]::ReadAllText((Join-Path $ManagedRoot 'managed-generations.json'));Remove-RetiredGenerations $current;$second=[IO.File]::ReadAllText((Join-Path $ManagedRoot 'managed-generations.json'));Assert ($first -eq $second) 'second cleanup changed ledger';Assert (Test-Path -LiteralPath $old) 'rerun deleted retained generation'
    }
    Run-Case 'ledger-external-directory-preserved' {
        New-Tree $ManagedRoot;$current=New-Generation 'current' @('KeepDb');$external=New-Generation 'external' @('RemovedDb');Set-Ledger @(@{Id='current';RelativePath='generations\current'},@{Id='external';RelativePath='..\outside'});$external2=Join-Path $ArtifactRoot 'outside';New-Item -ItemType Directory -Force -Path $external2|Out-Null;[IO.File]::WriteAllText((Join-Path $external2 'sentinel'),'keep');$threw=$false;try{Remove-RetiredGenerations $current}catch{$threw=$true};Assert $threw 'ledger traversal did not refuse';Assert (Test-Path -LiteralPath (Join-Path $external2 'sentinel')) 'outside directory was touched'
    }
    Run-Case 'json-timestamp-roundtrip-preserves-process-identity' {
        New-Tree $ManagedRoot;$directory=Join-Path $ManagedRoot 'timestamp-fixture';New-Item -ItemType Directory -Path $directory|Out-Null
        $currentProcess=Get-Process -Id $PID;$absentPid=[int]::MaxValue;Assert ($null -eq (Get-Process -Id $absentPid -ErrorAction SilentlyContinue)) 'The sentinel PID must not exist'
        $recordPath=Join-Path $directory 'active.json';Write-JsonFixture $recordPath @{ControllerPid=$PID;ControllerCreatedUtc=$currentProcess.StartTime.ToUniversalTime().ToString('O');ServerPid=$absentPid;ServerCreatedUtc=[DateTime]::UtcNow.ToString('O');Directory=$directory}
        $record=Get-Content -Raw -LiteralPath $recordPath|ConvertFrom-Json
        Stop-OwnedServer $record
        Assert (Test-Path -LiteralPath (Join-Path $directory 'server-stop')) 'The matching JSON process identity was rejected'
        Assert ($null -ne (Get-Process -Id $PID -ErrorAction SilentlyContinue)) 'The identity-only fixture process must stay alive'
    }
    Run-Case 'direct-server-record-has-no-controller' {
        New-Tree $ManagedRoot;$directory=Join-Path $ManagedRoot 'direct-pid-fixture';New-Item -ItemType Directory -Path $directory|Out-Null
        $absentPid=[int]::MaxValue;Assert ($null -eq (Get-Process -Id $absentPid -ErrorAction SilentlyContinue)) 'The sentinel PID must not exist'
        Stop-OwnedServer @{ControllerPid=0;ControllerCreatedUtc=$null;ServerPid=$absentPid;ServerCreatedUtc=[DateTime]::UtcNow;Directory=$directory}
        Assert (Test-Path -LiteralPath (Join-Path $directory 'server-stop')) 'A direct server record was rejected because it has no controller'
    }
    Run-Case 'pid-creation-mismatch-never-stops-process' {
        New-Tree $ManagedRoot;$directory=Join-Path $ManagedRoot 'pid-fixture';New-Item -ItemType Directory -Path $directory|Out-Null;$pidNow=$PID;$process=Get-Process -Id $pidNow;$wrong=$process.StartTime.ToUniversalTime().AddYears(-1).ToString('O');$record=@{ControllerPid=$pidNow;ControllerCreatedUtc=$wrong;ServerPid=0;ServerCreatedUtc=[DateTime]::UtcNow.ToString('O');Directory=$directory};$threw=$false;try{Stop-OwnedServer $record}catch{$threw=$true};Assert $threw 'PID creation mismatch did not throw';Assert (-not (Test-Path -LiteralPath (Join-Path $directory 'server-stop'))) 'stop marker was written for mismatched PID';$alive=Get-Process -Id $pidNow -ErrorAction SilentlyContinue;Assert ($null -ne $alive) 'test process stopped unexpectedly'
    }
    $rebuilds=@($ast.FindAll({param($node)$node -is [Management.Automation.Language.IfStatementAst] -and $node.Clauses[0].Item1.Extent.Text -eq '$Operation -in @(''Rebuild'',''All'')'},$true))
    if($rebuilds.Count -ne 1){throw 'Expected one Rebuild workflow.'}
    $text=$rebuilds[0].Clauses[0].Item2.Extent.Text
    $rebuildFlow=[scriptblock]::Create($text.Substring(1,$text.Length-2))
    function Invoke-RebuildFixture([bool]$defer,[bool]$failValidation,[bool]$failProof){
        New-Tree $ManagedRoot;[void](New-Generation 'previous' @('KeepDb'));Set-Ledger @(@{Id='previous';RelativePath='generations\previous'})
        $activePath=Join-Path $ManagedRoot 'active.json';Write-JsonFixture $activePath @{Generation='previous'};$before=[IO.File]::ReadAllText($activePath)
        $state=@{Calls=[Collections.Generic.List[string]]::new();FailValidation=$failValidation;FailProof=$failProof}
        # 外部の取得・ビルド・検証だけを置き換える。証跡そのものの検査は上の独立試験で確認する。
        function Get-PackageInfo{return @{Version='fixture'}}
        function Build-Consumer($packages,[string]$directory){return Join-Path $directory 'host.dll'}
        function Verify-Provenance($packages,[string]$directory){}
        function Execution-Locks([string]$directory){return @()}
        function Invoke-Step([string]$name,[string]$executable,[string[]]$arguments,[object[]]$resources){
            $state.Calls.Add($name)
            if($name -eq 'validate-package-tds-native' -and $state.FailValidation){throw 'fixture validation failed'}
        }
        function Get-ValidatedGeneration([string]$id){
            $state.Calls.Add('verify-proofs');if($state.FailProof){throw 'fixture proof failed'}
            Assert ($state.Calls.Contains('validate-package-tds-native')) 'Proof verification ran before validation'
            return @{Id=$id;Directory=(Resolve-Managed ('generations\'+$id))}
        }
        function Activate-ValidatedGeneration($candidate,[string]$verifier){$state.Calls.Add('activate');Write-JsonFixture $activePath @{Generation=$candidate.Id}}
        function Remove-RetiredGenerations([string]$directory){$state.Calls.Add('cleanup')}
        $Operation='Rebuild';$DeferActivation=$defer;$DatabaseName=@('KeepDb');$dotnet='fixture-dotnet'
        $threw=$false;try{& $rebuildFlow 6>$null|Out-Null}catch{$threw=$true}
        Assert ($threw -eq ($failValidation -or $failProof)) 'Unexpected workflow failure state'
        foreach($name in @('capture-source-and-generate','import-and-verify-all-data','deploy-query-module','validate-package-tds-native')){Assert ($state.Calls.Contains($name)) "Missing build operation: $name"}
        if($defer -or $threw){
            Assert (-not $state.Calls.Contains('activate') -and -not $state.Calls.Contains('cleanup')) 'Deferred or failed rebuild performed activation/cleanup'
            Assert ([IO.File]::ReadAllText($activePath) -ceq $before) 'Deferred or failed rebuild changed active.json'
        }else{
            Assert (($state.Calls.IndexOf('verify-proofs') -lt $state.Calls.IndexOf('activate')) -and ($state.Calls.IndexOf('activate') -lt $state.Calls.IndexOf('cleanup'))) 'Activation order differs'
            Assert ([IO.File]::ReadAllText($activePath) -cne $before) 'Normal rebuild did not activate'
        }
        Assert (Test-Path -LiteralPath (Join-Path $ManagedRoot 'generations\previous\.seamless-generation-owner')) 'Previous generation was changed by fixture'
    }
    Run-Case 'deferred-rebuild-validates-without-switch-or-cleanup' {Invoke-RebuildFixture $true $false $false}
    Run-Case 'normal-rebuild-retains-activation-order' {Invoke-RebuildFixture $false $false $false}
    Run-Case 'deferred-rebuild-validation-failure-retains-active' {Invoke-RebuildFixture $true $true $false}
    Run-Case 'deferred-rebuild-proof-failure-retains-active' {Invoke-RebuildFixture $true $false $true}
    Run-Case 'defer-on-other-operation-fails-before-output' {
        Assert (@($ast.ParamBlock.Parameters|Where-Object {$_.Name.VariablePath.UserPath -eq 'DeferActivation'}).Count -eq 1) 'Explicit defer option missing'
        $target=Join-Path $ArtifactRoot 'must-not-create';$threw=$false
        try{& $source -Operation Compare -DeferActivation -ManagedRoot $target 6>$null|Out-Null}catch{$threw=$_.Exception.Message -like '*supported only with -Operation Rebuild*'}
        Assert $threw 'Defer on Compare was not rejected';Assert (-not (Test-Path -LiteralPath $target)) 'Invalid option created output'
    }
}catch{Add-Result 'harness' 'Failed' $_.Exception.ToString()}
$resultArray=$results.ToArray();$failed=@($resultArray|Where-Object status -eq 'Failed');$summary=[pscustomobject]@{source=$source;artifactRoot=$ArtifactRoot;results=$resultArray;passed=@($resultArray|Where-Object status -eq 'Passed').Count;failed=$failed.Count;unavailable=@($resultArray|Where-Object status -eq 'Unavailable').Count}
$summary|ConvertTo-Json -Depth 20
if($failed.Count -gt 0){exit 1}
