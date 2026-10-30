# Seamless.slnx から、各 exe を実行するのに必要な依存だけを残した軽量 .slnx を生成する。
# 辿る辺: ProjectReference / csproj 内 <MSBuild Projects> (子プロセス Worker) / projitems Import (入れ子含む) / projitems と同ディレクトリの .shproj
# 参照を変更したら再実行し、生成された *.slnx もコミットすること。
$ErrorActionPreference = 'Stop'

$Solutions = [ordered]@{
    'Benchmark.TpcC.slnx' = @('Benchmark/Benchmark.TpcC/Benchmark.TpcC.csproj','Benchmark/Benchmark.TpcC.Tests/Benchmark.TpcC.Tests.csproj')
    'TestSeamless.slnx' = @('TestSeamless/TestSeamless.csproj','TestSeamless/Tests/TestSeamless.Tests.csproj')
    'Benchmark.Headless.slnx'                  = @('Benchmark/Benchmark.Headless/Benchmark.Headless.csproj')
    'Benchmark.Scalable.slnx'                  = @('Benchmark/Benchmark.Scalable.UI/Benchmark.Scalable.UI.csproj', 'Benchmark/Benchmark.Scalable.Headless/Benchmark.Scalable.Headless.csproj', 'Benchmark/Benchmark.Scalable.Tests/Benchmark.Scalable.Tests.csproj')
    'Benchmark.Tsavorite.ParameterSweep.slnx'  = @('Benchmark/Benchmark.Tsavorite.ParameterSweep.Headless/Benchmark.Tsavorite.ParameterSweep.Headless.csproj')
    'Benchmark.Dynamic.slnx'                   = @('Benchmark/Benchmark.Dynamic.UI/Benchmark.Dynamic.UI.csproj', 'Benchmark.Dynamic.Tests/Benchmark.Dynamic.Tests.csproj', 'Benchmark/Benchmark.Infrastructure.Tests/Benchmark.Infrastructure.Tests.csproj')
    'Seamless.Database.Tool.slnx'              = @('Seamless.Database.Tool/Seamless.Database.Tool.csproj')
    'Sample.NetShop.slnx'                      = @('Samples/Sample.NetShop/Sample.NetShop.csproj', 'Samples/Sample.NetShop.Tests/Sample.NetShop.Tests.csproj')
    'Sample.VirtualDb.slnx'                    = @('Samples/Sample.VirtualDb/Sample.VirtualDb.csproj')
    'Benchmark.TpcC.slnx'                      = @('Benchmark/Benchmark.TpcC.Tests/Benchmark.TpcC.Tests.csproj')
    'Benchmark.TpcE.slnx'                      = @('Benchmark/Benchmark.TpcE/Benchmark.TpcE.csproj', 'Benchmark/Benchmark.TpcE.Tests/Benchmark.TpcE.Tests.csproj', 'Benchmark/Benchmark.TpcE.Compliance/Benchmark.TpcE.Compliance.csproj', 'Benchmark/Benchmark.TpcE.Compliance.Tests/Benchmark.TpcE.Compliance.Tests.csproj', 'Benchmark/Benchmark.Tpc.Tests/Benchmark.Tpc.Tests.csproj')
    'Tests.slnx'                               = @(
        'Tests/Seamless.Transactions.Tests/Seamless.Transactions.Tests.csproj',
        'Tests/Seamless.Database.Transactions.Tests/Seamless.Database.Transactions.Tests.csproj',
        'Execution.Tests/Execution.Tests.csproj',
        'Benchmark/Benchmark.Infrastructure.Tests/Benchmark.Infrastructure.Tests.csproj',
        'Benchmark/Benchmark.Scalable.Tests/Benchmark.Scalable.Tests.csproj',
        'Test.Persisted.Collections/Test.Persisted.Collections.csproj',
        'Test.Scalable.Persisted.Collections/Test.Scalable.Persisted.Collections.csproj',
        'Test.Scalable.Tsavorite.Collections/Test.Scalable.Tsavorite.Collections.csproj',
        'Test.Tsavorite.Collections/Test.Tsavorite.Collections.csproj',
        'Test.Tsavorite.ParameterSweep/Test.Tsavorite.ParameterSweep.csproj',
        'Benchmark.Dynamic.Tests/Benchmark.Dynamic.Tests.csproj',
        'Benchmark.Dynamic.UITests/Benchmark.Dynamic.UITests.csproj',
        'Test/Test.csproj',
        'Benchmark/Benchmark.Headless.Tests/Benchmark.Headless.Tests.csproj',
        'Benchmark/Benchmark.Audit.Tests/Benchmark.Audit.Tests.csproj',
        'Benchmark/Benchmark.TpcC.Tests/Benchmark.TpcC.Tests.csproj',
        'Benchmark/Benchmark.TpcE.Tests/Benchmark.TpcE.Tests.csproj',
        'Benchmark/Benchmark.TpcE.Compliance.Tests/Benchmark.TpcE.Compliance.Tests.csproj',
        'Benchmark/Benchmark.Tpc.Tests/Benchmark.Tpc.Tests.csproj',
        'Benchmark/Benchmark.TpcC.Tests/Benchmark.TpcC.Tests.csproj',
        'Tests/Seamless.TestKit/Seamless.TestKit.csproj',
        'Samples/Sample.NetShop.Tests/Sample.NetShop.Tests.csproj'
    )
}

$Root = $PSScriptRoot
$MasterPath = Join-Path $Root 'Seamless.slnx'
$Errors = New-Object System.Collections.Generic.List[string]

function Get-RelativeKey([string]$fullPath) {
    $full = [System.IO.Path]::GetFullPath($fullPath)
    $rootFull = [System.IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    if (-not $full.StartsWith($rootFull, [StringComparison]::OrdinalIgnoreCase)) { throw "リポジトリ外のパス: $full" }
    return $full.Substring($rootFull.Length).Replace('\', '/')
}

# 実在する共有ソースを照合し、globを含まない接頭辞だけを正規化する。
function Test-CompilePath([string]$source,[string]$directory,[string]$include) {
    $raw=$include.Replace('$(MSBuildThisFileDirectory)',$directory+'\')
    if($raw -match '\$\('){return $false}
    $path=if([IO.Path]::IsPathRooted($raw)){$raw}else{Join-Path $directory $raw}
    if($raw -notmatch '[*?]'){
        return $source.Equals([IO.Path]::GetFullPath($path),[StringComparison]::OrdinalIgnoreCase)
    }
    $path=$path.Replace('\','/')
    $wildcard=$path.IndexOfAny([char[]]'*?')
    $separator=$path.LastIndexOf('/', $wildcard)
    $prefix=[IO.Path]::GetFullPath($path.Substring(0,$separator+1)).Replace('\','/').TrimEnd('/')+'/'
    $expression=[regex]::Escape($prefix)
    $segments=$path.Substring($separator+1).Split('/')
    for($i=0;$i -lt $segments.Length;$i++){
        $segment=$segments[$i]
        if($segment -eq '**'){
            if($i -lt $segments.Length-1){$expression+='(?:[^/]+/)*'}else{$expression+='.*'}
        }else{
            $expression+=[regex]::Escape($segment).Replace('\*','[^/]*').Replace('\?','[^/]')
            if($i -lt $segments.Length-1){$expression+='/'}
        }
    }
    return [regex]::IsMatch($source.Replace('\','/'),'^'+$expression+'$',[Text.RegularExpressions.RegexOptions]::IgnoreCase)
}

# ファイル内の参照先 (csproj / projitems) をリポジトリ相対パスで返す
function Get-Edges([string]$relPath) {
    $full = Join-Path $Root $relPath
    $dir = Split-Path $full -Parent
    $text = [System.IO.File]::ReadAllText($full)
    $patterns = @(
        '<ProjectReference\s[^>]*?Include\s*=\s*"([^"]+)"',
        '<MSBuild\s[^>]*?Projects\s*=\s*"([^"]+\.csproj)"',
        '<Import\s[^>]*?Project\s*=\s*"([^"]+\.projitems)"'
    )
    $result = @()
    foreach ($pattern in $patterns) {
        foreach ($m in [regex]::Matches($text, $pattern)) {
            $raw = $m.Groups[1].Value.Replace('$(MSBuildProjectDirectory)', $dir + '\').Replace('$(MSBuildThisFileDirectory)', $dir + '\')
            if ($raw -match '\$\(') { continue }
            $target = if ([System.IO.Path]::IsPathRooted($raw)) { $raw } else { Join-Path $dir $raw }
            if (-not (Test-Path -LiteralPath $target)) { $Errors.Add("参照先が存在しない: $relPath -> $raw"); continue }
            $result += Get-RelativeKey $target
        }
    }
    # 直接リンクされた共有ソースにも所有projitemsへの辺を張る。
    foreach($include in [regex]::Matches($text,'<Compile\s[^>]*?Include\s*=\s*"([^"*?]+\.cs)"')){
        $raw=$include.Groups[1].Value.Replace('$(MSBuildProjectDirectory)',$dir+'\').Replace('$(MSBuildThisFileDirectory)',$dir+'\')
        if($raw -match '\$\('){continue}
        $sourcePath=if([IO.Path]::IsPathRooted($raw)){$raw}else{Join-Path $dir $raw}
        $source=[IO.Path]::GetFullPath($sourcePath)
        if(-not (Test-Path -LiteralPath $source)){continue}
        $owner=[IO.DirectoryInfo](Split-Path $source -Parent)
        while($owner -and $owner.FullName.StartsWith($Root,[StringComparison]::OrdinalIgnoreCase)){
            $found=$false
            foreach($items in Get-ChildItem -LiteralPath $owner.FullName -Filter '*.projitems'){
                $itemsText=[IO.File]::ReadAllText($items.FullName)
                foreach($compile in [regex]::Matches($itemsText,'<Compile\s[^>]*?Include\s*=\s*"([^"]+)"[^>]*>')){
                    if(-not (Test-CompilePath $source $owner.FullName $compile.Groups[1].Value)){continue}
                    $excluded=$false
                    $exclude=[regex]::Match($compile.Value,'\sExclude\s*=\s*"([^"]+)"')
                    foreach($pattern in $exclude.Groups[1].Value.Split(';')){
                        if($pattern -and (Test-CompilePath $source $owner.FullName $pattern)){$excluded=$true;break}
                    }
                    if(-not $excluded){$result+=Get-RelativeKey $items.FullName;$found=$true;break}
                }
            }
            if($found){break}
            $owner=$owner.Parent
        }
    }
    # projitems は同ディレクトリの .shproj とセットで見せる (AGENTS.md の共有プロジェクト可視化方針)
    if ($relPath.EndsWith('.projitems', [StringComparison]::OrdinalIgnoreCase)) {
        foreach ($shproj in Get-ChildItem -LiteralPath $dir -Filter '*.shproj') { $result += Get-RelativeKey $shproj.FullName }
    }
    return $result
}

function Get-Closure([string[]]$roots) {
    $visited = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    $queue = New-Object System.Collections.Generic.Queue[string]
    foreach ($r in $roots) { if ($visited.Add($r)) { $queue.Enqueue($r) } }
    while ($queue.Count -gt 0) {
        $current = $queue.Dequeue()
        if ($current.EndsWith('.shproj', [StringComparison]::OrdinalIgnoreCase)) { continue }
        foreach ($next in Get-Edges $current) { if ($visited.Add($next)) { $queue.Enqueue($next) } }
    }
    return , $visited
}

[xml]$master = [System.IO.File]::ReadAllText($MasterPath)
$registered = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($node in $master.SelectNodes('//Project|//File')) { [void]$registered.Add($node.GetAttribute('Path')) }

function Test-Keep($element, $closure) {
    return ($element.LocalName -eq 'Project' -or $element.LocalName -eq 'File') -and $closure.Contains($element.GetAttribute('Path'))
}

$utf8 = New-Object System.Text.UTF8Encoding $false
foreach ($name in $Solutions.Keys) {
    foreach ($r in $Solutions[$name]) { if (-not (Test-Path -LiteralPath (Join-Path $Root $r))) { $Errors.Add("起点が存在しない: $name -> $r") } }
    $closure = Get-Closure $Solutions[$name]
    foreach ($p in $closure) {
        if (-not $registered.Contains($p)) { $Errors.Add("Seamless.slnx に未登録: $p (required by $name)") }
    }

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('<Solution>')
    foreach ($child in $master.DocumentElement.ChildNodes) {
        if ($child.NodeType -ne 'Element') { continue }
        if ($child.LocalName -eq 'Folder') {
            $kept = @($child.ChildNodes | Where-Object { $_.NodeType -eq 'Element' -and (Test-Keep $_ $closure) })
            if ($kept.Count -eq 0) { continue }
            $lines.Add("  <Folder Name=`"$($child.GetAttribute('Name'))`">")
            foreach ($k in $kept) { $lines.Add('    ' + $k.OuterXml) }
            $lines.Add('  </Folder>')
        }
        elseif (Test-Keep $child $closure) { $lines.Add('  ' + $child.OuterXml) }
    }
    $lines.Add('</Solution>')
    $content = ($lines -join "`r`n") + "`r`n"

    $outPath = Join-Path $Root $name
    $existing = if (Test-Path -LiteralPath $outPath) { [System.IO.File]::ReadAllText($outPath) } else { $null }
    if ($existing -ceq $content) { Write-Host "unchanged: $name" }
    else { [System.IO.File]::WriteAllText($outPath, $content, $utf8); Write-Host "written:   $name ($($closure.Count) entries)" }
}

if ($Errors.Count -gt 0) {
    $Errors | Sort-Object -Unique | ForEach-Object { Write-Error $_ -ErrorAction Continue }
    exit 1
}
