# VS と VS Code の設定を、ディスク上のプロジェクトに合わせて作り直す。
#   - Seamless.slnx : 未登録のプロジェクトを足し、消えたものを外し、移動したものを付け替える
#   - 絞り込み *.slnx : focused-solutions.json の起点から参照をたどって作り直す (Tests.slnx はテスト全部)
#   - .vscode/launch.json : 実行できるプロジェクトごとに「▶ 名前」の起動構成を作る。手書きの構成は残し、参照先だけ直す
#   - .vscode/tasks.json / Seamless.slnLaunch : 移動したプロジェクトの参照先を直す
#   - 共有プロジェクト (.projitems/.shproj) の規則を検査する
# -Check を付けると何も書き換えず、ずれがあれば終了コード 1 を返す。
# git の pre-commit フック (.githooks/pre-commit) から自動で呼ばれる。
param(
    [switch]$Check
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2

$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..')).TrimEnd('\')
Set-Location -LiteralPath $Root
# git のフックから呼ばれたとき日本語が文字化けしないように
try { [Console]::OutputEncoding = New-Object Text.UTF8Encoding $false } catch { }
$ConfigPath = Join-Path $PSScriptRoot 'focused-solutions.json'
$MasterName = 'Seamless.slnx'
$LaunchPrefix = [string][char]0x25B6 + ' '

$Errors = New-Object System.Collections.Generic.List[string]
$Changed = New-Object System.Collections.Generic.List[string]
$Notes = New-Object System.Collections.Generic.List[string]

# ---------------------------------------------------------------- ファイル入出力

function Get-FullPath([string]$rel) { return Join-Path $Root ($rel.Replace('/', '\')) }

function Read-TextFile([string]$rel) {
    $full = Get-FullPath $rel
    if (-not (Test-Path -LiteralPath $full)) { return $null }
    $bytes = [IO.File]::ReadAllBytes($full)
    $bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $text = (New-Object Text.UTF8Encoding $false).GetString($bytes)
    if ($bom) { $text = $text.Substring(1) }
    $newline = if ($text.Contains("`r`n")) { "`r`n" } elseif ($text.Contains("`n")) { "`n" } else { "`r`n" }
    return [pscustomobject]@{ Text = $text; Bom = $bom; Newline = $newline }
}

# 内容が変わるときだけ書く。-Check のときは書かずに記録だけする。
function Write-TextFile([string]$rel, [string]$text, [bool]$bom, [string]$newline) {
    $text = $text.Replace("`r`n", "`n")
    if ($newline -ne "`n") { $text = $text.Replace("`n", $newline) }
    $existing = Read-TextFile $rel
    if ($null -ne $existing -and $existing.Text -ceq $text) { return }
    $Changed.Add($rel)
    if ($Check) { return }
    $full = Get-FullPath $rel
    $dir = Split-Path $full -Parent
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }
    [IO.File]::WriteAllText($full, $text, (New-Object Text.UTF8Encoding $bom))
}

# ---------------------------------------------------------------- JSON (PowerShell 5.1 の ConvertTo-Json は < > ' を \u に変えるので自前で書く)

function ConvertTo-JsonString([string]$s) {
    $sb = New-Object Text.StringBuilder
    [void]$sb.Append('"')
    foreach ($ch in $s.ToCharArray()) {
        switch ($ch) {
            '"' { [void]$sb.Append('\"') }
            '\' { [void]$sb.Append('\\') }
            "`n" { [void]$sb.Append('\n') }
            "`r" { [void]$sb.Append('\r') }
            "`t" { [void]$sb.Append('\t') }
            default {
                if ([int]$ch -lt 0x20) { [void]$sb.AppendFormat('\u{0:x4}', [int]$ch) } else { [void]$sb.Append($ch) }
            }
        }
    }
    [void]$sb.Append('"')
    return $sb.ToString()
}

function ConvertTo-PrettyJson($value, [int]$level = 0, [int]$indent = 2) {
    $pad = ' ' * ($indent * ($level + 1))
    $end = ' ' * ($indent * $level)
    if ($null -eq $value) { return 'null' }
    if ($value -is [string]) { return ConvertTo-JsonString $value }
    if ($value -is [bool]) { if ($value) { return 'true' } else { return 'false' } }
    if ($value -is [int] -or $value -is [long] -or $value -is [double] -or $value -is [decimal]) { return [string]::Format([Globalization.CultureInfo]::InvariantCulture, '{0}', $value) }
    if ($value -is [System.Collections.IList]) {
        $items = @($value)
        if ($items.Count -eq 0) { return '[]' }
        $scalars = @($items | Where-Object { $_ -is [string] -or $_ -is [bool] -or $_ -is [int] -or $_ -is [double] })
        if ($scalars.Count -eq $items.Count) {
            $inline = '[' + ((@($items | ForEach-Object { ConvertTo-PrettyJson $_ 0 $indent })) -join ', ') + ']'
            if ($inline.Length + $pad.Length -le 110) { return $inline }
        }
        $parts = @($items | ForEach-Object { $pad + (ConvertTo-PrettyJson $_ ($level + 1) $indent) })
        return "[`n" + ($parts -join ",`n") + "`n$end]"
    }
    $props = @($value.PSObject.Properties)
    if ($value -is [System.Collections.IDictionary]) { $props = @($value.Keys | ForEach-Object { [pscustomobject]@{ Name = $_; Value = $value[$_] } }) }
    if ($props.Count -eq 0) { return '{}' }
    $parts = @($props | ForEach-Object { $pad + (ConvertTo-JsonString $_.Name) + ': ' + (ConvertTo-PrettyJson $_.Value ($level + 1) $indent) })
    return "{`n" + ($parts -join ",`n") + "`n$end}"
}

# ---------------------------------------------------------------- プロジェクトの列挙

$Config = (Read-TextFile 'tools/dev-env/focused-solutions.json').Text | ConvertFrom-Json
$DiscoveryExclude = @($Config.discoveryExclude)

function Test-Excluded([string]$rel) {
    if ($rel -match '(^|/)(bin|obj)/') { return $true }
    foreach ($prefix in $DiscoveryExclude) { if ($rel.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { return $true } }
    return $false
}

# git が管理しているもの + まだ追加していない新規ファイル (.gitignore で除外されたものは対象外)
function Get-RepoFiles([string[]]$patterns) {
    $output = & git -C $Root -c core.quotepath=off ls-files --cached --others --exclude-standard -- @patterns
    if ($LASTEXITCODE -ne 0) { throw 'git ls-files に失敗しました。' }
    return @($output | Where-Object { $_ -and -not (Test-Excluded $_) -and (Test-Path -LiteralPath (Get-FullPath $_)) } | Sort-Object -Unique)
}

$ProjectFiles = Get-RepoFiles @('*.csproj', '*.shproj')
$ProjItemsFiles = Get-RepoFiles @('*.projitems')
$ProjectsByName = @{}
foreach ($p in @($ProjectFiles) + @($ProjItemsFiles)) {
    $key = [IO.Path]::GetFileName($p).ToLowerInvariant()
    if (-not $ProjectsByName.ContainsKey($key)) { $ProjectsByName[$key] = New-Object System.Collections.Generic.List[string] }
    $ProjectsByName[$key].Add($p)
}

# 消えたパスの移動先を、ファイル名が一つだけ一致するものから探す
function Find-MovedProject([string]$rel, $alreadyUsed) {
    $key = [IO.Path]::GetFileName($rel.Replace('\', '/')).ToLowerInvariant()
    if (-not $ProjectsByName.ContainsKey($key)) { return $null }
    $candidates = @($ProjectsByName[$key] | Where-Object { $null -eq $alreadyUsed -or -not $alreadyUsed.Contains($_) })
    if ($candidates.Count -eq 1) { return $candidates[0] }
    return $null
}

function Test-RelExists([string]$rel) { return Test-Path -LiteralPath (Get-FullPath $rel) }

$ProjectTextCache = @{}
function Get-ProjectText([string]$rel) {
    if (-not $ProjectTextCache.ContainsKey($rel)) { $ProjectTextCache[$rel] = [IO.File]::ReadAllText((Get-FullPath $rel)) }
    return $ProjectTextCache[$rel]
}
function Test-TestProject([string]$rel) { return $rel.EndsWith('.csproj') -and (Get-ProjectText $rel) -match '<IsTestProject>\s*true\s*</IsTestProject>' }
function Get-OutputType([string]$rel) {
    $text = Get-ProjectText $rel
    $m = [regex]::Match($text, '<OutputType>\s*([^<\s]+)\s*</OutputType>')
    if ($m.Success) { return $m.Groups[1].Value }
    if ($text -match 'Sdk="Microsoft\.NET\.Sdk\.(Web|Worker)"') { return 'Exe' }
    return 'Library'
}

# ---------------------------------------------------------------- 0. プロジェクト間の参照
# プロジェクトを移動すると、ほかのプロジェクトからの ProjectReference / 共有プロジェクトの Import の相対パスが切れる。
# ファイル名が一つだけ一致する移動先があれば、相対パスを書き直す。

function Get-RelativePathText([string]$fromDir, [string]$toFile, [bool]$backslash) {
    $from = New-Object Uri ($fromDir.TrimEnd('\') + '\')
    $to = New-Object Uri $toFile
    $rel = [Uri]::UnescapeDataString($from.MakeRelativeUri($to).ToString())
    if ($backslash) { return $rel.Replace('/', '\') }
    return $rel
}

foreach ($project in @($ProjectFiles | Where-Object { $_.EndsWith('.csproj') }) + @($ProjItemsFiles)) {
    $file = Read-TextFile $project
    $dir = Split-Path (Get-FullPath $project) -Parent
    $pattern = '(?<head><(ProjectReference|Import)\s[^>]*?(Include|Project)\s*=\s*")(?<path>[^"$]+\.(csproj|shproj|projitems))(?<tail>")'
    $fixed = [regex]::Replace($file.Text, $pattern, {
            param($m)
            $raw = $m.Groups['path'].Value
            $target = [IO.Path]::GetFullPath((Join-Path $dir $raw))
            if (Test-Path -LiteralPath $target) { return $m.Value }
            $moved = Find-MovedProject $raw $null
            if (-not $moved) { return $m.Value }
            $newRaw = Get-RelativePathText $dir (Get-FullPath $moved) ($raw.Contains('\'))
            $Notes.Add("${project}: 参照の移動を反映 $raw -> $newRaw")
            return $m.Groups['head'].Value + $newRaw + $m.Groups['tail'].Value
        })
    if ($fixed -cne $file.Text) {
        Write-TextFile $project $fixed $file.Bom $file.Newline
        $ProjectTextCache[$project] = $fixed
    }
}

# ---------------------------------------------------------------- 1. Seamless.slnx

$masterFile = Read-TextFile $MasterName
[xml]$master = $masterFile.Text
$solution = $master.DocumentElement

$registered = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($node in @($solution.SelectNodes('//Project'))) { [void]$registered.Add($node.GetAttribute('Path')) }

# プロジェクトを先に見て、移動したディレクトリの対応 (旧 -> 新) を覚え、同じディレクトリにあった File も一緒に付け替える
$MovedDirs = [ordered]@{}
foreach ($node in @($solution.SelectNodes('//Project')) + @($solution.SelectNodes('//File'))) {
    $path = $node.GetAttribute('Path')
    if (Test-RelExists $path) { continue }
    $moved = $null
    if ($node.LocalName -eq 'Project') {
        $moved = Find-MovedProject $path $registered
        if ($moved) {
            $oldDir = [IO.Path]::GetDirectoryName($path).Replace('\', '/')
            $newDir = [IO.Path]::GetDirectoryName($moved).Replace('\', '/')
            if ($oldDir -and $newDir -and $oldDir -ne $newDir) { $MovedDirs[$oldDir] = $newDir }
        }
    }
    else {
        foreach ($oldDir in $MovedDirs.Keys) {
            if ($path.StartsWith($oldDir + '/', [StringComparison]::OrdinalIgnoreCase)) {
                $candidate = $MovedDirs[$oldDir] + $path.Substring($oldDir.Length)
                if (Test-RelExists $candidate) { $moved = $candidate; break }
            }
        }
    }
    if ($moved) {
        $node.SetAttribute('Path', $moved)
        [void]$registered.Add($moved)
        $Notes.Add("${MasterName}: 移動を反映 $path -> $moved")
    }
    else {
        [void]$node.ParentNode.RemoveChild($node)
        $Notes.Add("${MasterName}: 存在しないので外した $path")
    }
    [void]$registered.Remove($path)
}

function Get-DirSegments([string]$rel) {
    $parts = $rel.Split('/')
    if ($parts.Length -le 1) { return , @() }
    return , @($parts[0..($parts.Length - 2)])
}

$AddedNodes = New-Object 'System.Collections.Generic.HashSet[object]'
# 置き場所: テストは /Tests/ フォルダ。それ以外はディレクトリが最も近い登録済みの項目 (Project / File) の隣。無ければ直下。
function Add-SolutionEntry([string]$kind, [string]$path, $preferredAnchor) {
    $anchor = $preferredAnchor
    $parent = if ($anchor) { $anchor.ParentNode } else { $null }
    $testsFolder = $solution.SelectSingleNode("Folder[@Name='/Tests/']")
    if (-not $anchor -and $kind -eq 'Project' -and (Test-TestProject $path) -and $testsFolder) {
        $anchor = @($testsFolder.SelectNodes('Project')) | Select-Object -Last 1
        $parent = $testsFolder
    }
    if (-not $anchor) {
        # 共通するディレクトリが最も深い項目を集め、その項目が一番多く入っているフォルダの最後の項目の隣に置く
        $mine = Get-DirSegments $path
        $best = 0
        $nearest = @()
        foreach ($node in @($solution.SelectNodes('//Project|//File'))) {
            $theirs = Get-DirSegments $node.GetAttribute('Path')
            $common = 0
            while ($common -lt $mine.Count -and $common -lt $theirs.Count -and $mine[$common] -ieq $theirs[$common]) { $common++ }
            if ($common -gt $best) { $best = $common; $nearest = @($node) }
            elseif ($common -eq $best -and $common -gt 0) { $nearest += $node }
        }
        if ($nearest.Count -gt 0) {
            $group = $nearest | Group-Object { $_.ParentNode.GetHashCode() } | Sort-Object Count -Descending | Select-Object -First 1
            $anchor = @($group.Group) | Select-Object -Last 1
        }
        $parent = if ($anchor) { $anchor.ParentNode } else { $solution }
    }
    # 同じ場所に続けて足すときは、足した順に並ぶようにする
    while ($anchor -and $anchor.NextSibling -and $AddedNodes.Contains($anchor.NextSibling)) { $anchor = $anchor.NextSibling }
    $element = $master.CreateElement($kind)
    $element.SetAttribute('Path', $path)
    if ($anchor) { [void]$parent.InsertAfter($element, $anchor) }
    else {
        $lastRoot = @($solution.SelectNodes('Project')) | Select-Object -Last 1
        if ($lastRoot) { [void]$solution.InsertAfter($element, $lastRoot) } else { [void]$solution.AppendChild($element) }
    }
    [void]$AddedNodes.Add($element)
    $where = if ($parent -eq $solution) { '(直下)' } else { $parent.GetAttribute('Name') }
    $Notes.Add("${MasterName}: 追加 $path -> $where")
    return $element
}

# 本体のプロジェクトを先に登録してから、テストを登録する (テストの隣に本体が並ばないように)
$unregistered = @($ProjectFiles | Where-Object { -not $registered.Contains($_) })
foreach ($project in @($unregistered | Where-Object { -not (Test-TestProject $_) }) + @($unregistered | Where-Object { Test-TestProject $_ })) {
    [void](Add-SolutionEntry 'Project' $project $null)
    [void]$registered.Add($project)
}

# 共有プロジェクトの .projitems は、同じ場所の .shproj の隣に File として載せる (AGENTS.md の共有プロジェクト可視化方針)
$registeredFiles = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($node in @($solution.SelectNodes('//File'))) { [void]$registeredFiles.Add($node.GetAttribute('Path')) }
foreach ($items in (Get-RepoFiles @('*.projitems'))) {
    if ($registeredFiles.Contains($items)) { continue }
    $dir = [IO.Path]::GetDirectoryName($items).Replace('\', '/')
    $shprojNode = @($solution.SelectNodes('//Project') | Where-Object { $_.GetAttribute('Path').EndsWith('.shproj') -and [IO.Path]::GetDirectoryName($_.GetAttribute('Path')).Replace('\', '/') -eq $dir }) | Select-Object -First 1
    [void](Add-SolutionEntry 'File' $items $shprojNode)
    [void]$registeredFiles.Add($items)
}

foreach ($folder in @($solution.SelectNodes('Folder'))) {
    if (@($folder.ChildNodes | Where-Object { $_.NodeType -eq 'Element' }).Count -eq 0) { [void]$solution.RemoveChild($folder) }
}

function Format-Element($element, [string]$pad) {
    if ($element.LocalName -ne 'Folder') { return , @($pad + $element.OuterXml) }
    $attrs = (@($element.Attributes | ForEach-Object { "$($_.Name)=`"$([Security.SecurityElement]::Escape($_.Value))`"" })) -join ' '
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("$pad<Folder $attrs>")
    foreach ($child in $element.ChildNodes) {
        if ($child.NodeType -eq 'Element' -or $child.NodeType -eq 'Comment') { foreach ($l in (Format-Element $child ($pad + '  '))) { $lines.Add($l) } }
    }
    $lines.Add("$pad</Folder>")
    return , $lines.ToArray()
}

function Format-Solution($rootElement, [scriptblock]$keep) {
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('<Solution>')
    foreach ($child in $rootElement.ChildNodes) {
        if ($child.NodeType -ne 'Element' -and $child.NodeType -ne 'Comment') { continue }
        if ($child.LocalName -eq 'Folder') {
            $kept = @($child.ChildNodes | Where-Object { ($_.NodeType -eq 'Element' -or $_.NodeType -eq 'Comment') -and (& $keep $_) })
            if (@($kept | Where-Object { $_.NodeType -eq 'Element' }).Count -eq 0) { continue }
            $attrs = (@($child.Attributes | ForEach-Object { "$($_.Name)=`"$([Security.SecurityElement]::Escape($_.Value))`"" })) -join ' '
            $lines.Add("  <Folder $attrs>")
            foreach ($k in $kept) { foreach ($l in (Format-Element $k '    ')) { $lines.Add($l) } }
            $lines.Add('  </Folder>')
        }
        elseif (& $keep $child) { foreach ($l in (Format-Element $child '  ')) { $lines.Add($l) } }
    }
    $lines.Add('</Solution>')
    return ($lines -join "`n") + "`n"
}

Write-TextFile $MasterName (Format-Solution $solution { param($e) $true }) $masterFile.Bom $masterFile.Newline

# ---------------------------------------------------------------- 2. 絞り込みソリューション

function Get-RelativeKey([string]$fullPath) {
    $full = [IO.Path]::GetFullPath($fullPath)
    $rootFull = $Root + '\'
    if (-not $full.StartsWith($rootFull, [StringComparison]::OrdinalIgnoreCase)) { throw "リポジトリ外のパス: $full" }
    return $full.Substring($rootFull.Length).Replace('\', '/')
}

# ファイル内の参照先 (csproj / projitems) をリポジトリ相対パスで返す
# 辿る辺: ProjectReference / <MSBuild Projects> (子プロセスの Worker) / projitems の Import / 直接リンクした共有ソースの所有 projitems / projitems と同じ場所の .shproj
function Get-Edges([string]$relPath) {
    $full = Get-FullPath $relPath
    $dir = Split-Path $full -Parent
    $text = Get-ProjectText $relPath
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
            $target = if ([IO.Path]::IsPathRooted($raw)) { $raw } else { Join-Path $dir $raw }
            if (-not (Test-Path -LiteralPath $target)) { $Errors.Add("参照先が存在しない: $relPath -> $raw"); continue }
            $result += Get-RelativeKey $target
        }
    }
    foreach ($include in [regex]::Matches($text, '<Compile\s[^>]*?Include\s*=\s*"([^"*?]+\.cs)"')) {
        $raw = $include.Groups[1].Value.Replace('$(MSBuildProjectDirectory)', $dir + '\').Replace('$(MSBuildThisFileDirectory)', $dir + '\')
        if ($raw -match '\$\(' -or $raw -match ';' -or $raw.IndexOfAny([IO.Path]::GetInvalidPathChars()) -ge 0) { continue }
        try { $source = [IO.Path]::GetFullPath((Join-Path $dir $raw)) } catch { continue }
        if (-not (Test-Path -LiteralPath $source)) { continue }
        $owner = [IO.DirectoryInfo](Split-Path $source -Parent)
        while ($owner -and $owner.FullName.StartsWith($Root, [StringComparison]::OrdinalIgnoreCase)) {
            $found = $false
            foreach ($items in Get-ChildItem -LiteralPath $owner.FullName -Filter '*.projitems') {
                $itemsText = [IO.File]::ReadAllText($items.FullName)
                foreach ($compile in [regex]::Matches($itemsText, '<Compile\s[^>]*?Include\s*=\s*"([^"]+)"')) {
                    $pattern = $compile.Groups[1].Value.Replace('$(MSBuildThisFileDirectory)', $owner.FullName + '\')
                    if ($pattern -match '\$\(') { continue }
                    try { $pattern = [IO.Path]::GetFullPath((Join-Path $owner.FullName $pattern)) } catch { continue }
                    if ($source -like $pattern) { $result += Get-RelativeKey $items.FullName; $found = $true; break }
                }
            }
            if ($found) { break }
            $owner = $owner.Parent
        }
    }
    if ($relPath.EndsWith('.projitems', [StringComparison]::OrdinalIgnoreCase)) {
        foreach ($shproj in Get-ChildItem -LiteralPath $dir -Filter '*.shproj') { $result += Get-RelativeKey $shproj.FullName }
    }
    return $result
}

$EdgeCache = @{}
function Get-Closure([string[]]$roots) {
    $visited = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    $queue = New-Object System.Collections.Generic.Queue[string]
    foreach ($r in $roots) { if ($visited.Add($r)) { $queue.Enqueue($r) } }
    while ($queue.Count -gt 0) {
        $current = $queue.Dequeue()
        if ($current.EndsWith('.shproj', [StringComparison]::OrdinalIgnoreCase)) { continue }
        if (-not $EdgeCache.ContainsKey($current)) { $EdgeCache[$current] = @(Get-Edges $current) }
        foreach ($next in $EdgeCache[$current]) { if ($visited.Add($next)) { $queue.Enqueue($next) } }
    }
    return , $visited
}

$allTests = @($solution.SelectNodes('//Project') | ForEach-Object { $_.GetAttribute('Path') } | Where-Object { Test-TestProject $_ })
$masterEntries = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($node in @($solution.SelectNodes('//Project|//File'))) { [void]$masterEntries.Add($node.GetAttribute('Path')) }

$configChanged = $false
$SolutionsToRemove = New-Object System.Collections.Generic.List[string]
foreach ($property in @($Config.solutions.PSObject.Properties)) {
    $name = $property.Name
    $roots = New-Object System.Collections.Generic.List[string]
    $newDefinition = New-Object System.Collections.Generic.List[string]
    foreach ($entry in @($property.Value)) {
        if ($entry -eq '@tests') { $newDefinition.Add($entry); foreach ($t in $allTests) { $roots.Add($t) }; continue }
        $resolved = $entry
        if (-not (Test-RelExists $entry)) {
            $resolved = Find-MovedProject $entry $null
            if ($resolved) { $Notes.Add("focused-solutions.json: 移動を反映 $entry -> $resolved"); $configChanged = $true }
            else { $Notes.Add("focused-solutions.json: 消えた起点を外した $entry ($name)"); $configChanged = $true; continue }
        }
        $newDefinition.Add($resolved)
        $roots.Add($resolved)
    }
    if ($roots.Count -eq 0 -and -not ($newDefinition -contains '@tests')) {
        # 起点が全部消えた絞り込みソリューションは、定義ごと消す (生成物なので中身は失われない)
        $Notes.Add("focused-solutions.json: 起点が無くなったので $name を消した")
        $SolutionsToRemove.Add($name)
        $configChanged = $true
        continue
    }
    $property.Value = $newDefinition.ToArray()
    $closure = Get-Closure $roots.ToArray()
    foreach ($p in $closure) { if (-not $masterEntries.Contains($p)) { $Errors.Add("${MasterName} に未登録: $p ($name が必要としている)") } }
    $existing = Read-TextFile $name
    $bom = if ($existing) { $existing.Bom } else { $false }
    $content = Format-Solution $solution { param($e) ($e.LocalName -eq 'Project' -or $e.LocalName -eq 'File') -and $closure.Contains($e.GetAttribute('Path')) }
    Write-TextFile $name $content $bom "`r`n"
}
foreach ($name in $SolutionsToRemove) {
    $Config.solutions.PSObject.Properties.Remove($name)
    if (Test-RelExists $name) {
        $Changed.Add($name)
        if (-not $Check) { Remove-Item -LiteralPath (Get-FullPath $name) -Force }
    }
}
if ($configChanged) {
    $cfg = Read-TextFile 'tools/dev-env/focused-solutions.json'
    Write-TextFile 'tools/dev-env/focused-solutions.json' ((ConvertTo-PrettyJson $Config) + "`n") $cfg.Bom $cfg.Newline
}

# ---------------------------------------------------------------- 3. VS Code の起動構成とタスク

# 文字列の中のプロジェクト/ソリューションのパスが消えていたら、移動先に付け替える
function Repair-PathText([string]$text, [string]$where) {
    if ($null -eq $text) { return $text }
    $pattern = '(?<pre>\$\{workspaceFolder\}[\\/])?(?<path>[A-Za-z0-9_.\-\\/]+\.(csproj|shproj|slnx))'
    return [regex]::Replace($text, $pattern, {
            param($m)
            $raw = $m.Groups['path'].Value
            $rel = $raw.Replace('\', '/')
            if ($m.Groups['pre'].Success -or -not [IO.Path]::IsPathRooted($raw)) {
                if (Test-RelExists $rel) { return $m.Value }
                $moved = Find-MovedProject $rel $null
                if ($moved) {
                    $Notes.Add("${where}: 移動を反映 $rel -> $moved")
                    $out = if ($raw.Contains('\')) { $moved.Replace('/', '\') } else { $moved }
                    return $m.Groups['pre'].Value + $out
                }
                $Errors.Add("${where}: 参照先が見つからない $raw")
            }
            return $m.Value
        })
}

# JSON の書式 (改行・インデント・1 行の配列) はそのままに、文字列の中のパスだけを直す
function Repair-JsonText([string]$text, [string]$where) {
    return [regex]::Replace($text, '"(?:[^"\\]|\\.)*"', {
            param($m)
            $decoded = $m.Value.Substring(1, $m.Value.Length - 2).Replace('\\', [string][char]0).Replace('\"', '"').Replace([string][char]0, '\')
            if ($decoded -notmatch '\.(csproj|shproj|slnx)') { return $m.Value }
            $repaired = Repair-PathText $decoded $where
            if ($repaired -ceq $decoded) { return $m.Value }
            return ConvertTo-JsonString $repaired
        })
}

function Repair-Strings($value, [string]$where) {
    if ($value -is [string]) { return Repair-PathText $value $where }
    if ($value -is [System.Collections.IList]) { return , @($value | ForEach-Object { Repair-Strings $_ $where }) }
    if ($value -is [pscustomobject]) {
        foreach ($p in @($value.PSObject.Properties)) { $p.Value = Repair-Strings $p.Value $where }
    }
    return $value
}

$tasksFile = Read-TextFile '.vscode/tasks.json'
$tasksText = Repair-JsonText $tasksFile.Text '.vscode/tasks.json'
if ($tasksText -cne $tasksFile.Text) { Write-TextFile '.vscode/tasks.json' $tasksText $tasksFile.Bom $tasksFile.Newline }
$tasks = $tasksText | ConvertFrom-Json

# 手書きの coreclr 構成: 前処理タスクがビルドするプロジェクトの出力先から program/cwd を作り直す
$MsbuildCache = @{}
function Get-ExePath([string]$projectRel, [string]$configuration) {
    $key = "$projectRel|$configuration"
    if ($MsbuildCache.ContainsKey($key)) { return $MsbuildCache[$key] }
    $text = Get-ProjectText $projectRel
    $extra = @()
    if ($text -notmatch '<TargetFramework>' -and $text -match '<TargetFrameworks>\s*([^;<\s]+)') { $extra += "-p:TargetFramework=$($Matches[1])" }
    $json = & dotnet msbuild (Get-FullPath $projectRel) -nologo "-p:Configuration=$configuration" @extra '-getProperty:TargetDir' '-getProperty:AssemblyName' '-getProperty:UseAppHost' 2>$null
    if ($LASTEXITCODE -ne 0) { $Errors.Add("出力先を調べられない: $projectRel ($configuration)"); $MsbuildCache[$key] = $null; return $null }
    $props = (($json -join "`n") | ConvertFrom-Json).Properties
    $exe = $props.AssemblyName + $(if ($props.UseAppHost -eq 'false') { '.dll' } else { '.exe' })
    $dir = Get-RelativeKey $props.TargetDir
    $result = [pscustomobject]@{ Program = '${workspaceFolder}/' + $dir + $exe; Cwd = '${workspaceFolder}/' + $dir.TrimEnd('/') }
    $MsbuildCache[$key] = $result
    return $result
}

$launchFile = Read-TextFile '.vscode/launch.json'
$launch = $launchFile.Text | ConvertFrom-Json
$manual = New-Object System.Collections.Generic.List[object]
foreach ($c in @($launch.configurations)) {
    if ($c.name.StartsWith($LaunchPrefix)) { continue }
    $c = Repair-Strings $c '.vscode/launch.json'
    $hasPreLaunch = $c.PSObject.Properties.Name -contains 'preLaunchTask'
    if ($c.type -eq 'coreclr' -and $hasPreLaunch -and $c.preLaunchTask) {
        $task = @($tasks.tasks | Where-Object { $_.label -eq $c.preLaunchTask }) | Select-Object -First 1
        if (-not $task) { $Errors.Add(".vscode/launch.json: '$($c.name)' の前処理タスク '$($c.preLaunchTask)' が tasks.json に無い") }
        else {
            $proj = @($task.args | Where-Object { $_ -match '\.csproj$' }) | Select-Object -First 1
            if ($proj) {
                $projRel = $proj.Replace('${workspaceFolder}/', '').Replace('\', '/')
                $configuration = if (@($task.args) -contains 'Release') { 'Release' } else { 'Debug' }
                if (Test-RelExists $projRel) {
                    $exe = Get-ExePath $projRel $configuration
                    if ($exe) {
                        if ($c.program -ne $exe.Program) { $Notes.Add(".vscode/launch.json: '$($c.name)' の program を $($exe.Program) に更新"); $c.program = $exe.Program }
                        if ($c.PSObject.Properties.Name -contains 'cwd' -and $c.cwd -ne $exe.Cwd) { $c.cwd = $exe.Cwd }
                    }
                }
            }
        }
    }
    $manual.Add($c)
}

$launchExclude = @($Config.launchExclude)
$generated = New-Object System.Collections.Generic.List[object]
$runnable = @($solution.SelectNodes('//Project') | ForEach-Object { $_.GetAttribute('Path') } | Where-Object {
        $p = $_
        $p.EndsWith('.csproj') -and -not (Test-TestProject $p) -and (Get-OutputType $p) -match '^(Win)?Exe$' -and
        -not (@($launchExclude | Where-Object { $p.StartsWith($_, [StringComparison]::OrdinalIgnoreCase) }).Count)
    })
$nameCount = @{}
foreach ($p in $runnable) { $n = [IO.Path]::GetFileNameWithoutExtension($p); $nameCount[$n] = 1 + [int]$nameCount[$n] }
foreach ($p in $runnable) {
    $n = [IO.Path]::GetFileNameWithoutExtension($p)
    if ($nameCount[$n] -gt 1) { $n = "$n ($([IO.Path]::GetDirectoryName($p).Replace('\', '/')))" }
    $generated.Add([pscustomobject][ordered]@{
            name        = $LaunchPrefix + $n
            type        = 'dotnet'
            request     = 'launch'
            projectPath = '${workspaceFolder}/' + $p
        })
}
$sortedGenerated = @($generated | Sort-Object -Property name)
$newLaunch = [ordered]@{ version = $launch.version; configurations = @($manual.ToArray() + $sortedGenerated) }
foreach ($p in @($launch.PSObject.Properties)) { if ($p.Name -ne 'version' -and $p.Name -ne 'configurations') { $newLaunch[$p.Name] = $p.Value } }
Write-TextFile '.vscode/launch.json' ((ConvertTo-PrettyJson $newLaunch) + "`n") $launchFile.Bom $launchFile.Newline


# ---------------------------------------------------------------- 4. VS の起動プロファイル

$slnLaunchFile = Read-TextFile 'Seamless.slnLaunch'
if ($slnLaunchFile) {
    $after = Repair-JsonText $slnLaunchFile.Text 'Seamless.slnLaunch'
    if ($after -cne $slnLaunchFile.Text) { Write-TextFile 'Seamless.slnLaunch' $after $slnLaunchFile.Bom $slnLaunchFile.Newline }
}

# ---------------------------------------------------------------- 5. 共有プロジェクトの規則 (AGENTS.md)

foreach ($items in (Get-RepoFiles @('*.projitems'))) {
    $dir = [IO.Path]::GetDirectoryName($items).Replace('\', '/')
    $shprojs = @($ProjectFiles | Where-Object { $_.EndsWith('.shproj') -and [IO.Path]::GetDirectoryName($_).Replace('\', '/') -eq $dir })
    if ($shprojs.Count -eq 0) { $Errors.Add("共有プロジェクトの .shproj が無い: $items"); continue }
    $guid = [regex]::Match((Get-ProjectText $items), '<SharedGUID>\s*([^<\s]+)\s*</SharedGUID>').Groups[1].Value.Trim('{}')
    $ok = $false
    foreach ($s in $shprojs) {
        $text = Get-ProjectText $s
        if ($text -match [regex]::Escape($guid) -and $text -match [regex]::Escape([IO.Path]::GetFileName($items))) { $ok = $true }
    }
    if (-not $ok) { $Errors.Add("共有プロジェクトの GUID かファイル名が .projitems と合わない: $items") }
}

# ---------------------------------------------------------------- 結果

foreach ($n in ($Notes | Select-Object -Unique)) { Write-Host "  $n" }
if ($Changed.Count -gt 0) {
    $verb = if ($Check) { '更新が必要' } else { '更新した' }
    foreach ($c in $Changed) { Write-Host "${verb}: $c" }
}
else { Write-Host 'VS / VS Code の設定はプロジェクト構成と一致しています。' }
$uniqueErrors = @($Errors | Sort-Object -Unique)
foreach ($e in $uniqueErrors) { Write-Host "エラー: $e" -ForegroundColor Red }
if ($uniqueErrors.Count -gt 0) { exit 2 }
if ($Check -and $Changed.Count -gt 0) {
    Write-Host '直すには: powershell -NoProfile -ExecutionPolicy Bypass -File tools/dev-env/Sync-DevEnvironment.ps1'
    exit 1
}
exit 0
