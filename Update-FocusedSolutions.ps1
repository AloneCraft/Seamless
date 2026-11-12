# 互換用の入口。本体は tools/dev-env/Sync-DevEnvironment.ps1 (絞り込みソリューションの起点は tools/dev-env/focused-solutions.json)。
# git の pre-commit フックが自動で実行するので、普段は手で呼ぶ必要はない。
& (Join-Path $PSScriptRoot 'tools\dev-env\Sync-DevEnvironment.ps1') @args
exit $LASTEXITCODE
