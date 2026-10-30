param([string]$RepositoryRoot=(Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath($RepositoryRoot)
function Get-PackageProjects{
    [xml]$solution=[IO.File]::ReadAllText((Join-Path $repo 'Seamless.slnx'));$visited=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase);$ordered=New-Object 'System.Collections.Generic.List[string]'
    function Visit([string]$path){$full=[IO.Path]::GetFullPath($path);if(-not $visited.Add($full)){return};[xml]$project=[IO.File]::ReadAllText($full);foreach($edge in $project.SelectNodes('//ProjectReference')){$include=$edge.GetAttribute('Include');if($include -match '\$\('){throw "Cannot resolve package graph edge: $include"};Visit (Join-Path (Split-Path $full -Parent) $include)};if(-not (@($project.SelectNodes('//IsRoslynComponent')|Where-Object InnerText -eq 'true').Count -gt 0 -and @($project.SelectNodes('//IsPackable')|Where-Object InnerText -eq 'true').Count -eq 0)){$ordered.Add($full)}}
    foreach($node in $solution.SelectNodes('//Project')){$relative=$node.GetAttribute('Path');if($relative -like 'Seamless.*/*.csproj'){[xml]$project=[IO.File]::ReadAllText((Join-Path $repo $relative));if(@($project.SelectNodes('//IsPackable')|Where-Object InnerText -eq 'true').Count -gt 0){Visit (Join-Path $repo $relative)}}}
    return @($ordered)
}
Get-PackageProjects
