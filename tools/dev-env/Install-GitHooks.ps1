# このリポジトリの git フック (.githooks) を有効にする。クローンし直したときに 1 回だけ実行する。
#   pre-commit : プロジェクト構成が変わったら VS / VS Code の設定を自動で直してコミットに含める
#   commit-msg : コミットの件名に日本語が無ければ止める
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
git -C $root config core.hooksPath .githooks
if ($LASTEXITCODE -ne 0) { throw 'git config に失敗しました。' }
Write-Host "git フックを有効にしました (core.hooksPath = .githooks)。"
