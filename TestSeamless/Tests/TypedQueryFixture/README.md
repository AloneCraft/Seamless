# 型付きクエリの統合回帰

`Invoke-TypedQueryFixture.ps1` は実生成された COUNT の通常実行・事前キャンセル・途中キャンセル、手書き集計の direct／型付き client／remote 比較、FLinq interceptor と各行キャンセル、上限付きソートのディスク退避を検証します。実行時のプロジェクトと生成物は `SEAMLESS_COMPARISON_TEMP_ROOT` の親ディレクトリに、ログ・receipt は `SEAMLESS_COMPARISON_ARCHIVE_ROOT` に一意な ID で保存します。永続 csproj は追加しません。

Windows の共有開発環境では、登録済みの `BGO_MAILBOX` と共通 `BGO_STATE` を指定して実行します。最新製品 feed は archive 内の `latest-packages.json` から読みます。`PackageVersion` と `PackageFeed` の両方を渡せば明示指定できます。`dependency-feed` と公式 NuGet が復元に必要です。

```powershell
& ./TestSeamless/Tests/TypedQueryFixture/Invoke-TypedQueryFixture.ps1
```

隔離された CI ホストで broker を使用しない場合だけ `-Direct` を明示できます。実行には .NET 10、PowerShell、ローカル TCP ポート 35479 が必要です。
