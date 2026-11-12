[CmdletBinding()]
param([switch]$ValidateOnly)

$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
& (Join-Path $root 'TestSeamless/Invoke-LocalProcess.ps1') -InitializeOnly | Out-Null
$repository = 'AloneCraft/Seamless'
$api = "repos/$repository"
$sourceRef = & git -C $root branch --show-current
if ($LASTEXITCODE -ne 0 -or -not $sourceRef) { throw 'A source branch is required.' }

function Invoke-GitHub([string]$Endpoint, [string]$Method = 'GET', $Body = $null, [switch]$AllowNotFound) {
    if ($null -eq $Body) {
        $result = & gh api $Endpoint --method $Method
    } else {
        $result = ($Body | ConvertTo-Json -Depth 20 -Compress) | & gh api $Endpoint --method $Method --input -
    }
    if ($LASTEXITCODE -ne 0) {
        if ($AllowNotFound -and (($result | ConvertFrom-Json).status -eq '404')) { return $null }
        throw "GitHub API failed: $Method $Endpoint"
    }
    if ($result) { return ($result | ConvertFrom-Json) }
}

$pages = @(
    @{ Source = 'README.md'; Target = 'index.md'; Title = 'はじめに' }
    @{ Source = 'docs/FLinq.md'; Target = 'flinq.md'; Title = 'FLinq' }
    @{ Source = 'docs/execution-and-database.md'; Target = 'execution-and-database.md'; Title = '実行・データベース' }
    @{ Source = 'docs/profiles.md'; Target = 'profiles.md'; Title = '初期設定と拡張' }
    @{ Source = 'docs/verification.md'; Target = 'verification.md'; Title = '検証方法' }
    @{ Source = 'docs/クエリ一時作業コレクション.md'; Target = 'query-collections.md'; Title = 'クエリ一時作業コレクション' }
)
$files = [ordered]@{}
foreach ($page in $pages) {
    $content = [IO.File]::ReadAllText((Join-Path $root $page.Source))
    # Resolve relative Markdown links against the source document, not the generated site.
    $sourceDirectory = [IO.Path]::GetDirectoryName($page.Source).Replace('\', '/')
    $content = [regex]::Replace($content, '(?<prefix>\]\()(?<url>[^)\s]+)(?<suffix>\))', {
        param($match)
        $url = $match.Groups['url'].Value
        if ($url -match '^(?:[a-zA-Z][a-zA-Z0-9+.-]*:|#|/)') { return $match.Value }
        $parts = $url -split '#', 2
        $basePath = if ($sourceDirectory) { "$sourceDirectory/" } else { '' }
        $resolved = [Uri]::new([Uri]"https://source.invalid/$basePath", $parts[0]).AbsolutePath.TrimStart('/')
        $resolved = [Uri]::UnescapeDataString($resolved)
        $destination = $pages | Where-Object Source -CEQ $resolved | Select-Object -First 1
        if ($destination) {
            $href = $destination.Target -replace '\.md$', '.html'
        } else {
            $kind = if ($resolved.EndsWith('/')) { 'tree' } else { 'blob' }
            $encoded = (($resolved -split '/') | ForEach-Object { [Uri]::EscapeDataString($_) }) -join '/'
            $href = "https://github.com/$repository/$kind/main/$encoded"
        }
        if ($parts.Count -gt 1) { $href += '#' + $parts[1] }
        return '](' + $href + ')'
    })
    $files[$page.Target] = "---`nlayout: default`ntitle: $($page.Title)`n---`n`n$content"
}
$files['_config.yml'] = @'
title: Seamless ドキュメント
lang: ja
url: https://alonecraft.github.io
baseurl: /Seamless
markdown: kramdown
kramdown:
  input: GFM
  syntax_highlighter: rouge
exclude:
  - publish-documentation.ps1
'@
$navigation = ($pages | ForEach-Object {
    $target = $_.Target -replace '\.md$', '.html'
    '<a href="{{ ''/' + $target + ''' | relative_url }}">' + $_.Title + '</a>'
}) -join "`n"
$files['_layouts/default.html'] = @'
<!doctype html>
<html lang="ja">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{{ page.title | escape }} | {{ site.title | escape }}</title>
  <style>
    :root { color-scheme: light dark; font-family: system-ui, sans-serif; line-height: 1.8; }
    body { margin: 0; color: #243247; background: #f7f9fc; }
    header { padding: 1.2rem 2rem; background: #142c47; color: white; }
    header a { color: white; text-decoration: none; font-size: 1.3rem; font-weight: 700; }
    .shell { display: grid; grid-template-columns: 240px minmax(0, 1fr); max-width: 1440px; margin: auto; }
    nav { padding: 2rem 1rem; }
    nav a { display: block; padding: .5rem .75rem; border-radius: 6px; }
    a { color: #145da5; overflow-wrap: anywhere; }
    a:hover { text-decoration: underline; }
    main { min-width: 0; padding: 2rem 3rem; background: white; }
    h1, h2, h3 { line-height: 1.4; scroll-margin-top: 1rem; }
    h2 { border-bottom: 1px solid #dce3ed; padding-bottom: .5rem; margin-top: 2.5rem; }
    pre { overflow-x: auto; padding: 1rem; background: #eef2f7; border-radius: 8px; }
    code { font-family: ui-monospace, Consolas, monospace; font-size: .9em; }
    :not(pre) > code { background: #eef2f7; padding: .15em .3em; border-radius: 3px; }
    table { display: block; overflow-x: auto; border-collapse: collapse; }
    td, th { border: 1px solid #dce3ed; padding: .5rem .8rem; text-align: left; }
    footer { margin-top: 3rem; border-top: 1px solid #dce3ed; padding-top: 1rem; font-size: .85rem; }
    @media (max-width: 800px) { .shell { display: block; } nav { padding: 1rem; } nav a { display: inline-block; } main { padding: 1.5rem; } }
    @media (prefers-color-scheme: dark) { body { color: #e0e6ef; background: #111923; } main { background: #182330; } a { color: #8bc3ff; } pre, :not(pre) > code { background: #101923; } td, th, h2, footer { border-color: #3c4a5d; } }
  </style>
</head>
<body>
  <header><a href="{{ '/' | relative_url }}">Seamless ドキュメント</a></header>
  <div class="shell">
    <nav aria-label="ドキュメント">NAVIGATION</nav>
    <main id="main-content">{{ content }}<footer><a href="https://github.com/AloneCraft/Seamless">GitHub</a> · <a href="https://github.com/AloneCraft/Seamless/issues">質問・不具合報告</a></footer></main>
  </div>
</body>
</html>
'@ -replace 'NAVIGATION', $navigation

# Check the generated publication allowlist and all internal page links before any writes.
if ($files.Count -ne 8) { throw 'Unexpected publication file count.' }
foreach ($entry in $files.GetEnumerator()) {
    foreach ($match in [regex]::Matches($entry.Value, '\]\(([^)\s]+)\)')) {
        $link = $match.Groups[1].Value -split '#', 2
        if ($link[0] -match '^[^:/]+\.html$' -and -not $files.Contains(($link[0] -replace '\.html$', '.md'))) {
            throw "Broken site link in $($entry.Key): $($link[0])"
        }
    }
}
if ($ValidateOnly) {
    $files.Keys
    Write-Output 'Validation passed: six documents, layout, configuration, and internal page links.'
    return
}

$sourceSha = & git -C $root rev-parse HEAD
$settings = Invoke-GitHub "$api/pages" -AllowNotFound
if ($settings -and ($settings.source.branch -ne 'gh-pages' -or $settings.source.path -ne '/')) {
    throw 'Existing Pages source differs; review before changing it.'
}
$references = @(Invoke-GitHub "$api/git/matching-refs/heads/gh-pages")
$existing = $references | Where-Object ref -EQ 'refs/heads/gh-pages' | Select-Object -First 1
$treeEntries = @($files.GetEnumerator() | ForEach-Object {
    @{ path = $_.Key; mode = '100644'; type = 'blob'; content = $_.Value }
})
$tree = Invoke-GitHub "$api/git/trees" 'POST' @{ tree = $treeEntries }
$commitBody = @{ message = "Publish user documentation from $sourceRef ($sourceSha); selected working-tree documents"; tree = $tree.sha }
if ($existing) { $commitBody.parents = @($existing.object.sha) }
$commit = Invoke-GitHub "$api/git/commits" 'POST' $commitBody
if ($existing) {
    Invoke-GitHub "$api/git/refs/heads/gh-pages" 'PATCH' @{ sha = $commit.sha; force = $false } | Out-Null
} else {
    Invoke-GitHub "$api/git/refs" 'POST' @{ ref = 'refs/heads/gh-pages'; sha = $commit.sha } | Out-Null
}
$settings = Invoke-GitHub "$api/pages" -AllowNotFound
if (-not $settings) {
    try {
        Invoke-GitHub "$api/pages" 'POST' @{ build_type = 'legacy'; source = @{ branch = 'gh-pages'; path = '/' } } | Out-Null
    } catch {
        # Creating gh-pages can enable Pages asynchronously before the POST arrives.
        $settings = Invoke-GitHub "$api/pages"
        if ($settings.source.branch -ne 'gh-pages' -or $settings.source.path -ne '/') { throw }
    }
} elseif ($settings.source.branch -ne 'gh-pages' -or $settings.source.path -ne '/') {
    throw 'Existing Pages source differs; review before changing it.'
}
Write-Output "Published source commit: $($commit.sha)"
Write-Output 'Site: https://alonecraft.github.io/Seamless/'
