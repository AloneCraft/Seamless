# SQL Server 互換のシステムカタログ層

## 目的

SSMS でのテーブルの読み書き、ビューの読み取り、ストアドと関数の実行、DDL を、本物の SQL Server と同じ形で受け付ける。
SSMS が送る SQL を見つけるたびに足りない列や関数を1つずつ足すやり方はやめる。
代わりに、本物の SQL Server が持つシステムオブジェクトを丸ごと写し取り、全部の形を先に用意する。
SSMS の要求記録(`SEAMLESS_TDS_REQUEST_LOG`)は、何を作るかを決める材料ではなく、作った後の確認に使う。

## 決めたこと

- 形(名前・列・型・引数・結果の列)は本物の SQL Server から写し取り、`G:\GitHub\Seamless\Seamless.Database.SqlServer\SystemCatalog` に置く。
  MySQL を扱う場合は `Seamless.Database.MySql` に同じ構成で置く。
- 中身は Seamless のカタログ1つから作る。システムビュー用に別の情報源を持たない。
- 本物の T-SQL 本文(Microsoft が書いたシステムストアド・関数・ビューの定義)はリポジトリに入れない。
  写し取り時に `F:\Seamless\SystemCatalog\<版>\source` へ書き出し、T-SQL→C# 変換の入力にだけ使う。
- ユーザーのストアドと関数は、T-SQL を解析して C# に変換する。単純なシステムストアドと関数も同じ変換にかける。

## 段階

1. 写し取り(済み)
   `Export-SqlServerSystemCatalog.ps1` で本物から読み取り、TSV にする。`SqlServerSystemCatalog` が埋め込み資源から読む。
2. 全ビュー・全表を形どおりに用意する。中身は次のどれかで埋める。
   - Seamless のカタログから作る(objects、columns、indexes、parameters、databases など)
   - 決まった値の表(型、照合順序、設定値など)
   - Seamless に無い機能の表は0行にする(グラフ表、台帳、可用性グループ、全文検索など)
   - 実行状態の表(`dm_`)は最低限の値にする
   - 写し取りに無い列の値は、型に応じた既定値(0、偽、NULL)にする
   今の `SystemCatalogEmulator.cs` の文字列一致は、普通の問い合わせ処理がこれらの表を読む形へ置き換える。
3. 組み込み関数(`OBJECTPROPERTY`、`COLUMNPROPERTY`、`HAS_PERMS_BY_NAME`、`SERVERPROPERTY` など)を取りうる値の全種類で作り、式の中でも使えるようにする。
   組み込み関数はカタログに載らないので、一覧は公式文書から作る。
4. 言語機能(`DECLARE`、`IF`、`WHILE`、一時表、表変数、`INSERT … EXEC`、`TRY/CATCH`)。
5. ストアドと関数の中身。T-SQL のものは変換にかける。拡張ストアド(`xp_`)は必要なものだけ C# で書き、残りは本物と同じ番号の「未対応」エラーを返す。

## 写し取りの範囲と中身(SQL Server 17.0.1000.7)

| 範囲 | 意味 | 主な数 |
|---|---|---|
| `system` | `sys` と `INFORMATION_SCHEMA`。どのデータベースから見ても同じ | ビュー643、システム表79、ストアド1,497、拡張ストアド359、関数151 |
| `master` | master の dbo にある出荷時オブジェクト | 表5、ビュー1、ストアド2 |
| `msdb` | msdb の dbo にある出荷時オブジェクト | 表145、ビュー78、ストアド453、関数44 |

ファイルは `SystemCatalog\SqlServer17` にある。

- `objects.tsv`: オブジェクト
- `columns.tsv`: 列
- `parameters.tsv`: 引数。0番は関数の戻り値
- `result_columns.tsv`: ストアドの最初の結果の列
- `source.txt`: 写し取り元の版

`objects.tsv` の `result_set` 欄は、ストアドの結果の形が分かるかどうかを表す。

- `columns`: 結果の列が分かる。列は `result_columns.tsv` にある
- `none`: 結果を返さない
- `error:<理由>`: 実行しないと形が分からない。理由は一時表や動的SQLなど
- 空欄: ストアドではない

本物の多くのストアドは `error:` で、結果の形は段階5で変換したコードから決まる。

文字列の列の照合順序は、写し取り元のサーバーの既定値(`Japanese_CI_AS`)のまま記録している。
使う側では、各データベースの既定値へ置き換える。

## 作り直し方

本物の SQL Server を読み取るだけで、変更はしない。
CPU を8論理に制限する `TestSeamless\Invoke-LocalProcess.ps1` 経由で動かす。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File G:\GitHub\Seamless\Seamless.Database.SqlServer\SystemCatalog\Export-SqlServerSystemCatalog.ps1 -OutputDirectory G:\GitHub\Seamless\Seamless.Database.SqlServer\SystemCatalog\SqlServer17 -SourceDirectory F:\Seamless\SystemCatalog\17.0.1000.7\source
```

版を変えたときは、`SqlServerSystemCatalogTests` の件数と版の期待値も見直す。

## 名乗る版

写し取り元と同じ 17.0.1000.7(SQL Server 2025)を名乗る。2026-11-01 に 16.0 から変更した(ユーザー指示)。
SSMS は相手の版を見て送る SQL を変えるので、形と名乗りがずれないようにする。
値は `Seamless.Database.Gateway\Sql\SqlServerVersion.cs` の1か所に置き、次のすべてがここから出る。

- PreLogin と LOGINACK の版
- `SERVERPROPERTY` の `ProductVersion`・`ProductMajorVersion`・`ProductMinorVersion`・`ProductBuild`
- `@@VERSION`
- SSMS のサーバー情報の問い合わせ(`VersionMajor`・`VersionMinor`・`BuildNumber`)
- `sys.databases` の `compatibility_level`(170)

版を上げるときは、写し取りをやり直し、`SqlServerVersion.cs` を直し、版を固定している試験(`ServerPropertyWireTests`、`GatewayServerTests`、`SsmsInitializationTests`)も直す。

## 段階2で見つけた組み込み関数の不具合(修正済)

SSMSの「上位1000行の選択」が送るSMOの問い合わせは、ビューのIsSystemObjectなどを遅延取得する際に
`OBJECTPROPERTY(..., 'ExecIsAnsiNullsOn')` のような、Seamlessが実装していないプロパティ名を渡す。
Seamlessはこれを例外(40515)にしていたが、本物のSQL Serverは未知・対象外のプロパティ名には単に`NULL`を返すだけだった(実機で確認済み)。
`Seamless.Database.Gateway\Hosting\DatabaseRegistry.cs` の `OBJECTPROPERTY`/`OBJECTPROPERTYEX` 実装を、未対応のプロパティ名では`NULL`を返すように修正した。

この種の例外が「列メタデータを送った後・行を送る前」に起きると、エラー応答の追い打ちでTDS接続が壊れ、
次の要求が `already open DataReader` のようなクライアント側エラーになることが分かった。
今回は発生源(未対応プロパティでの例外)を直したので起きなくなったが、
`TdsSession.cs`/`TdsPacketWriter.cs` 側に「行評価中の例外で接続が壊れる」という一般的な弱さが残っている。
将来、別の行評価(式評価)が例外を投げると同じ症状が再発し得るので、段階3以降で対応を検討する。

## 未決事項

- 変換後の C# をリポジトリに入れてよいか。
- TdsSession/TdsPacketWriterの「行評価中の例外で接続が壊れる」弱さを直すか。
