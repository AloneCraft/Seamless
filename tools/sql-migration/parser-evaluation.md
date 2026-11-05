# SqlParserCS 0.6.5 構文試験

`parser-evaluation.ps1` は明示的に指定した SqlParserCS 0.6.5 の net8.0 DLL を PowerShell から呼び、SQL Server、PostgreSQL、MySQL の基本構文・不正構文・型変換・位置情報を検査する。SQL の実行や速度計測は行わない。

公式 NuGet `SqlParserCS` 0.6.5 の `lib/net8.0/SqlParser.dll` の SHA-256 は `0C80F92BC658C0A55FEB3F9A51FBCB5EC48368A0389D609EC1C3FEA8AC886518`。スクリプトは標準でこのハッシュと assembly version 0.6.5.0 を検証する。パッケージには `Microsoft.Extensions.ObjectPool` 8.0.1 の依存宣言がある。PowerShell のランタイム上で依存が利用可能な環境を使う。異なる DLL を明示的に評価する場合のみ `-ExpectedSha256` を指定する。

```powershell
pwsh -NoProfile -File G:\GitHub\Seamless\tools\sql-migration\parser-evaluation.ps1 `
  -DllPath F:\CodexTemp\SeamlessSqlOssTrial\package\lib\net8.0\SqlParser.dll `
  -ResultPath F:\CodexTemp\SeamlessSqlOssTrial\repo-results.json
```

出力先の親ディレクトリは事前に用意し、D: または F: を指定する。JSON に DLL ハッシュ、runtime、全 SQL、診断、検査結果を保存し、検査失敗時は終了コード 1 を返す。BGO 参加時はリポジトリ source の S と出力の X を取得する。短時間の既存 DLL 試験であり build・ベンチマークはしない。

基本 39 ケースに加え各方言の CAST、TOP/LIMIT の寛容性、不完全な CAST と ASCII/BMP/絵文字/CRLF の診断位置を確認する。AST の型と選択したプロパティを直接検査する。`CAST('abc' AS INT)` と重複列は構文として受理されても意味検証や実行で正しいとは限らない。方言を指定しても T-SQL の LIMIT と PostgreSQL/MySQL の TOP はこの版では受理される。結果から全構文や実行互換性を推定しない。Unicode の診断位置はこの入力に限った観測であり、一般的な位置規約を保証しない。
