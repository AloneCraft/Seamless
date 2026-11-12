[CmdletBinding()]
param([string]$Suffix='final1')
$ErrorActionPreference='Stop'
$checks=@(
    @{Project='Benchmark/Benchmark.Dynamic.UI/Benchmark.Dynamic.UI.csproj';Name='ui-debug';Action='Build'},
    @{Project='Benchmark/Benchmark.Infrastructure.Tests/Benchmark.Infrastructure.Tests.csproj';Name='infrastructure-debug';Framework='net10.0'},
    @{Project='Benchmark/Benchmark.Infrastructure.Tests/Benchmark.Infrastructure.Tests.csproj';Name='infrastructure-windows-debug';Framework='net10.0-windows'},
    @{Project='Tests/Database.Verification.Tests/Database.Verification.Tests.csproj';Name='database-verification-debug'},
    @{Project='Tests/Database.Compilation.Tests/Database.Compilation.Tests.csproj';Name='database-compilation-release';Configuration='Release'},
    @{Project='Tests/Database.Query.Tests/Database.Query.Tests.csproj';Name='database-query-release';Configuration='Release'},
    @{Project='Tests/Seamless.Database.Transactions.Tests/Seamless.Database.Transactions.Tests.csproj';Name='database-transactions-release';Configuration='Release'},
    @{Project='Tests/Database.Verification.Tests/Database.Verification.Tests.csproj';Name='database-verification-release';Configuration='Release'},
    @{Project='Test.Tsavorite.Collections/Test.Tsavorite.Collections.csproj';Name='tsavorite-release';Configuration='Release';Filter='FullyQualifiedName~Test.Seamless'},
    @{Project='Test.Persisted.Collections/Test.Persisted.Collections.csproj';Name='persisted-release';Configuration='Release';Filter='FullyQualifiedName~Test.Seamless'},
    @{Project='Execution.Tests/Execution.Tests.csproj';Name='execution-release';Configuration='Release'},
    @{Project='Benchmark.Dynamic.Tests/Benchmark.Dynamic.Tests.csproj';Name='dynamic-core-release';Configuration='Release';CoreOnly=$true},
    @{Project='Benchmark.Dynamic.Tests/Benchmark.Dynamic.Tests.csproj';Name='dynamic-windows-debug';Filter='Category!=ChainCorpusFull&Category!=Survey'},
    @{Project='Benchmark.Dynamic.Tests/Benchmark.Dynamic.Tests.csproj';Name='dynamic-windows-release';Configuration='Release';Filter='Category!=ChainCorpusFull&Category!=Survey'},
    @{Project='Benchmark/Benchmark.Infrastructure.Tests/Benchmark.Infrastructure.Tests.csproj';Name='infrastructure-windows-release';Framework='net10.0-windows';Configuration='Release'},
    @{Project='Benchmark.Dynamic.UITests/Benchmark.Dynamic.UITests.csproj';Name='ui-driver-availability'}
)
$failed=@()
foreach($check in $checks){
    $check.Name+='-'+$Suffix
    $receipt=Join-Path 'F:\CodexTemp\test-retirement-20261104' ($check.Name+'/receipt.json')
    if(Test-Path -LiteralPath $receipt){
        $result=Get-Content -Raw -LiteralPath $receipt | ConvertFrom-Json
        if($result.Outcome -eq 'Passed'){Write-Output "Previously passed: $($check.Name)";continue}
        throw "A failed receipt already exists. Use a new suffix: $($check.Name)"
    }
    Write-Output "Checking: $($check.Name)"
    & "$PSScriptRoot/Invoke-TestMigrationCheck.ps1" @check -TimeoutSeconds 2400
    $result=Get-Content -Raw -LiteralPath $receipt | ConvertFrom-Json
    if($result.Outcome -ne 'Passed'){$failed+=$check.Name}
}
if($failed.Count){throw ('Failed checks: '+($failed -join ', '))}
