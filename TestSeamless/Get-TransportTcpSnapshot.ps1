param(
    [Parameter(Mandatory)][ValidateRange(1,65535)][int]$TargetPort,
    [Parameter(Mandatory)][ValidateRange(1,65535)][int]$Ipv4First,
    [Parameter(Mandatory)][ValidateRange(1,65535)][int]$Ipv4Last,
    [Parameter(Mandatory)][ValidateRange(1,65535)][int]$Ipv6First,
    [Parameter(Mandatory)][ValidateRange(1,65535)][int]$Ipv6Last
)
$ErrorActionPreference='Stop'
if($Ipv4Last -lt $Ipv4First -or $Ipv6Last -lt $Ipv6First){throw 'Invalid dynamic port range'}

$started=[DateTimeOffset]::UtcNow
$connections=@(Get-NetTCPConnection -ErrorAction Stop)
$finished=[DateTimeOffset]::UtcNow
$families=foreach($family in 'IPv4','IPv6'){
    $isIpv4=$family -eq 'IPv4'
    if($isIpv4){$first=$Ipv4First;$last=$Ipv4Last}
    else{$first=$Ipv6First;$last=$Ipv6Last}
    $local=@($connections|Where-Object {
        $_.LocalPort -ge $first -and $_.LocalPort -le $last -and ($_.LocalAddress -notlike '*:*') -eq $isIpv4
    })
    $groups=@{Target=[Collections.Generic.List[object]]::new();Other=[Collections.Generic.List[object]]::new();Bound=[Collections.Generic.List[object]]::new()}
    foreach($entry in $local){
        if($entry.State -eq 'Bound'){
            $groups.Bound.Add($entry)
        }else{
            if($entry.RemotePort -eq $TargetPort){
                $remote=[Net.IPAddress]::Parse($entry.RemoteAddress)
                if($remote.IsIPv4MappedToIPv6){$remote=$remote.MapToIPv4()}
                if([Net.IPAddress]::IsLoopback($remote)){$groups.Target.Add($entry);continue}
            }
            $groups.Other.Add($entry)
        }
    }
    $counts=foreach($name in 'Target','Other','Bound'){
        $entries=$groups[$name]
        [ordered]@{Category=$name;Rows=$entries.Count;DistinctLocalPorts=@($entries|ForEach-Object LocalPort|Sort-Object -Unique).Count;
            States=@($entries|Group-Object -Property State|ForEach-Object {[ordered]@{State=$_.Name;Count=$_.Count}})}
    }
    [ordered]@{Family=$family;First=$first;Last=$last;Rows=$local.Count;DistinctLocalPorts=@($local|ForEach-Object LocalPort|Sort-Object -Unique).Count;Groups=@($counts)}
}
[ordered]@{StartedUtc=$started;CompletedUtc=$finished;TargetPort=$TargetPort;Families=@($families)}|ConvertTo-Json -Depth 7 -Compress
