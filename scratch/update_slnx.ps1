$content = Get-Content 'Seamless.slnx'
$filtered = $content | Where-Object { $_ -notmatch 'Execution\.Tests/Execution\.Tests\.csproj' }
$filtered | Set-Content 'Seamless.slnx' -Encoding UTF8
Write-Host "Updated Seamless.slnx. New line count: $($filtered.Count)"
