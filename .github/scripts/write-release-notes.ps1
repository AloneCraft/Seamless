# Release のノート(Markdown)を生成する。変更内容は直前の v* タグからのコミット件名。
param(
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$Output
)
$ErrorActionPreference = 'Stop'

[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)   # git の出力(UTF-8)を文字化けさせずに受ける
$ErrorActionPreference = 'Continue'   # Windows PowerShell 5.1 は native の stderr を例外にするため
$prev = git describe --tags --abbrev=0 --match 'v*' 'HEAD^' 2>$null
$ErrorActionPreference = 'Stop'
$range = if ($prev) { "$prev..HEAD" } else { 'HEAD' }
$changes = (git log $range --no-merges --pretty='- %s (%h)' | Select-Object -First 100) -join "`n"

$fence = '```'
$notes = @"
## Seamless v$Version

.NET 10 向けの Seamless ライブラリ群(NuGet パッケージ)です。nuget.org には公開していません。下のアセットを取得して使います。

### インストール

1. アセットの ``*.nupkg`` を同じフォルダにダウンロードする
2. そのフォルダをローカル NuGet フィードとして追加してパッケージを入れる

${fence}
dotnet nuget add source <ダウンロードしたフォルダ> --name seamless-local
dotnet add package Seamless --version $Version
${fence}

データベース取り込みツール(``seamless`` コマンド):

${fence}
dotnet tool install --global Seamless.Database.Tool --version $Version --add-source <ダウンロードしたフォルダ>
${fence}

### アセット

- ``Seamless*.nupkg`` / ``*.snupkg``: ライブラリ・ジェネレータ・ツールとデバッグ用シンボル(``Seamless`` を入れれば一式、個別でも入れられます)
- ``Seamless-Samples-$Version.zip``: ``Samples/`` のソース(ビルド済みではありません。サンプルのビルドには MAUI のワークロードが必要)
- ``SHA256SUMS.txt``: 全アセットのチェックサム

対応: .NET 10

### 変更内容

$changes
"@
$path = if ([System.IO.Path]::IsPathRooted($Output)) { $Output } else { Join-Path (Get-Location).Path $Output }
[System.IO.File]::WriteAllText($path, $notes, [System.Text.UTF8Encoding]::new($false))
