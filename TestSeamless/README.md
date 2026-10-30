# SQL Server / SeamlessDB 比較環境

SQL Server を正として、型付きモデル・保存データ・SQL ビュー・ネイティブクエリモジュールを構築世代ごとに生成します。元のデータベース名、スキーマ名、オブジェクト名と C# 識別子の対応は `generation.json` に保存します。主キーなしテーブルの重複行は非公開の取り込み行 ID で保存します。

## 準備と再実行

Windows、.NET 10 SDK、PowerShell、ローカル SQL Server を利用します。入口スクリプトは PowerShell 7 以降が必要で、Windows PowerShell 5.1 では実行前に拒否します。TPC-E の EGen 1.14.0 は `Work/TpcE/egen` と `Work/TpcE/egen-build/EGenLoader.exe`、SQL Server 用投入処理は `Preparation` にあります。

接続文字列は環境変数、`Seamless.Comparison.Local` のユーザーシークレット、実行ファイル側 `appsettings.json` の順で指定できます。環境変数を最優先します。資格情報は世代ファイルへ保存しません。

```powershell
$env:ConnectionStrings__Source='Server=localhost;Database=master;Integrated Security=True;Encrypt=False;TrustServerCertificate=True'

# 明示的な初期準備。TPC_E と書き込み専用 fixture を用意する。
pwsh -File ./TestSeamless/Invoke-Comparison.ps1 -Operation Prepare

# 製品とその依存を同一の新規版で作成し、専用ローカルフィードへ保存する。
pwsh -File ./TestSeamless/Invoke-Comparison.ps1 -Operation Pack -Configuration Release

# 毎回ユーザーDBを検出し、新しい世代を構築する。
pwsh -File ./TestSeamless/Invoke-Comparison.ps1 -Operation Rebuild -Configuration Release

# SSMS操作中など、構築・全検証だけ行って切り替えを後にする（PowerShell 7から実行）。
& ./TestSeamless/Invoke-Comparison.ps1 -Operation Rebuild -DatabaseName @('AdventureWorks2016','Seamless_Comparison_Fixture') -DeferActivation -Configuration Release

# 検証済み世代を再開する。
pwsh -File ./TestSeamless/Invoke-Comparison.ps1 -Operation Activate -GenerationId <世代ID> -Configuration Release

# 現在の稼働世代で正しさを再確認してから性能測定する。
pwsh -File ./TestSeamless/Invoke-Comparison.ps1 -Operation Compare -Configuration Release

# 初期準備を繰り返さず Pack / Rebuild / Compare を順に行う。
pwsh -File ./TestSeamless/Invoke-Comparison.ps1 -Operation All -Configuration Release
```

`-Offline` は外部依存の `.nupkg` が `ArchiveRoot/dependency-feed` にすべてある場合だけ使用します。外部依存が足りなければ復元は失敗します。製品依存は必ず今回のフィードと版から復元します。

各工程は共有調停器を使わず、子孫を含む同じ最大8論理CPUのaffinityで直接実行します。標準出力・標準エラーと終了結果は操作ごとにFへ保存します。同じ管理領域のパイプラインはファイルlockで重複実行を防ぎます。Activate は検証済み世代の証跡と元DB一覧・スキーマを再確認し、同じ製品版の独立 bootstrap を使用してから世代の元EXEを直接起動します。

`Rebuild -DeferActivation` は通常と同じ取り込み・全件検証・証跡確認を行い、Validated世代を残します。稼働サーバー停止、`active.json`更新、SSMS資産公開、旧世代削除は行いません。後で表示された世代IDを指定してActivateします。このオプションはRebuild専用で、All等への誤指定は出力前に拒否します。

取り込み済み世代の検証だけを再実行する場合は `pwsh -File ./TestSeamless/Invoke-Comparison.ps1 -Operation Validate -GenerationId <世代ID> -Offline` を使います。既存の検証コピーとレポートは所有情報・絶対パス・排他lockを確認して `validation-attempts` へ保持し、元データを再取り込みせず利用側EXEの再ビルド・モジュール配置・全検証を行います。成功後の稼働切り替えは別のActivate操作です。

通常の再構築は `TPC_E` を作成しません。削除したユーザーDBは次の一覧から外れます。OFFLINE、権限不足、接続失敗は削除に置き換えず構築失敗にします。検証の最後に元DBの全一覧と対象スキーマを再取得し、変化があれば公開しません。旧世代の削除は、この最終確認済みの全DB一覧を基準にします。`-DatabaseName` で選ばなかったDBを削除済みと扱いません。全一覧または最終確認の証跡がない世代では削除を拒否します。書き込み試験は `Seamless_Comparison_Fixture` に限定し、元のサンプルDBにデータ変更を行いません。

ローカルの既定保存先は、SSD の `D:/Seamless/Comparison/Managed`（DB・世代別ビルド）、`D:/Seamless/Comparison/Temp`（一時領域）、HDD の `F:/Seamless/Comparison`（フィード・パッケージ・操作ログ）です。C ドライブへの出力は拒否します。ソースコードはリポジトリに維持します。`-ManagedRoot`、`-ArchiveRoot`、`-TemporaryRoot`、環境変数 `SEAMLESS_COMPARISON_ARCHIVE_ROOT`／`SEAMLESS_COMPARISON_TEMP_ROOT` で変更できます。D/F がない CI ではリポジトリ作業領域を使用します。依存パッケージの復元元も F の `dependency-feed` と NuGet 公式フィードにし、新規の復元先は F の専用キャッシュにします。比較環境は共有BGOを利用せず、所有する計算処理を共通の最大8論理CPUで直接実行します。以前の共有台帳や互換リンクは他の利用者の資産として扱い、この工程では変更しません。`generations/<世代>`、`active.json` を管理します。生成されたコード、データ、SQL 定義、パッケージ証跡、クエリ DLL、検証結果は各世代に属します。全検証に成功するまで前の稼働構成を維持し、新サーバーの正常性確認まで失敗した場合は前の構成へ戻します。他者が起動したプロセスは停止しません。

## パッケージとテストソリューション

`TestSeamless.slnx` は `Update-FocusedSolutions.ps1` が生成します。EXE、自動テスト、Schema、Queries の4プロジェクトを含みます。製品参照は `PackageReference`、テスト用プロジェクト間は `ProjectReference` です。製品のプロジェクトや `bin` のDLLへの参照は使いません。

切り替え成功時に `Comparison.Local.props` を生成し、Visual Studio で開く版・生成コード・ローカルフィードを稼働世代へ合わせます。このローカル設定には資格情報を含めず、Gitには登録しません。パッケージ作成前の初回復元はできないため、入口スクリプトでパッケージを先に作成してください。

保存先の環境変数は `Initialize-Storage.ps1` が子プロセスへ設定します。手動のテスト実行でも先にこのスクリプトを呼び、TEMP/TMP・NuGet・dotnet の作業先を適用してください。

型付きクエリの恒久統合テストは [TypedQueryFixture](Tests/TypedQueryFixture/README.md) にあります。その実行で作ったパッケージの版とフィードを渡し、同じ最大8論理CPUの直接実行helperで起動します。COUNT の途中キャンセル、手書き集計の直接・リモート実行、FLinq 生成、上限付きソートのディスク退避を検証します。

`package-provenance.json` は復元先の専用キャッシュ・版・フィードを検証した結果、`flinq-proof.json` は生成されたインターセプターのハッシュです。型付きクエリ定義から、ビルド前に普通の C# としてサーバー入口とクライアントを生成します。詳細は `Queries/README.md` を参照してください。

## 実行経路

| 経路 | 接続・実行方法 |
|---|---|
| SQL Server | 元サーバーへの SqlClient 接続 |
| SeamlessDB SQL | 既定は TDS `127.0.0.1,14333`、SQL Server 認証 `sa`、空以外のテスト用パスワード、暗号化なし |
| 型付き直接 | 同一世代の検証用データコピーと同じビルド済みクエリモジュール |
| 型付きリモート | `NativeQueryClient`、`localhost:14335`、MemoryPack の長さ付きフレーム |
| HTTP | `http://localhost:8088/api/databases`、`/api/sql?database=<DB名>` |

比較環境では、このマシンの既定 SqlClient で `localhost` が接続前にタイムアウトしたため、SeamlessDB SQL の既定接続先を `127.0.0.1,14333` にしています。テスト EXE の接続文字列は `ConnectionStrings__Seamless` で上書きできます。これは比較環境の設定であり、製品のネットワークや認証の既定値を変更するものではありません。

動的 SQL はサーバーの共通 SQL コンポーネントで解析・型検査・実行計画化します。動的 SQL を実行時に FLinq コンパイルしません。ネイティブではクエリ ID と引数だけを送信し、DLLはスクリプトで一度配置します。製品版、スキーマ指紋、DLLハッシュの不適合は拒否します。

SSMS と EXE は `Cases` の SQL を共用します。稼働 EXE で SQL ファイルを直接実行する例は次のとおりです。

```powershell
dotnet <世代のTestSeamless.dll> sql source Seamless_Comparison_Fixture ./TestSeamless/Cases/Common/Read-AggregateJoinView.sql
dotnet <世代のTestSeamless.dll> sql seamless Seamless_Comparison_Fixture ./TestSeamless/Cases/Common/Read-AggregateJoinView.sql
```

元DBへの直接ファイル実行は読み取り文を制限し、専用 fixture だけに CRUD・取引を許します。SSMS 22 のソリューションは `Ssms/TestSeamless.Compare.sln` です。`SQLServer` と `SeamlessDB` の2プロジェクトの接続・生成SQLは、検証済み世代をステージングしてから公開します。生成SQLの所有台帳とハッシュを照合し、手書きファイルや編集済み生成物を削除しません。

## 正しさと計測

全テーブルの値・件数・NULL・重複数を同一ソース取引内で照合します。ビューは元定義を実行します。順序未指定結果は外部ソートで比較し、浮動小数点計算由来と確定した列だけ許容誤差を使い、直接投影・由来不明の列は厳密に比較します。手書きケースは `ComputedFloatingPointOrdinals` に0始まりの計算列番号を指定でき、`ComputedFloatingPoint=true` だけの場合は全浮動列が計算由来であるという明示指定になります。SQL・型付き直接・型付きリモートのすべてで、指定した並べ替えキーの順序を検証します。整数・decimal・文字列・日時・取り込み値は厳密です。許容誤差は環境変数 `SEAMLESS_COMPARISON_FLOAT_TOLERANCE` に JSON（`SingleAbsolute`、`SingleRelative`、`DoubleAbsolute`、`DoubleRelative`）を指定して変更でき、負値・非有限値は拒否します。使用値を取り込み・比較レポートへ保存します。性能レポートには実行したSQLケースと型付き引数のMemoryPackバイト列も保存します。巨大行や一時領域の上限超過は失敗にします。

メモリ最適化テーブルを含む元DBでは session SNAPSHOT を使えません。Serializable 取引内で disk 表の読み取りロックを取得し、メモリ最適化表へ SNAPSHOT ヒントを付け、元DBの設定を変更せず一貫した読み取りを行います。参照: [Microsoft の41332説明](https://learn.microsoft.com/en-us/sql/relational-databases/errors-events/mssqlserver-41332-database-engine-error?view=sql-server-ver17)、[メモリ最適化テーブルの取引](https://learn.microsoft.com/en-us/sql/relational-databases/in-memory-oltp/transactions-with-memory-optimized-tables?view=sql-server-ver17)。

性能は Release でウォームアップ2回、測定7回、同時接続1・4・16を既定にします。SQL送信から全件受信までを測り、照合は区間外です。CPU時間、Working Set／Private Bytesの観測ピーク、プロセスI/O転送量を記録します。I/O転送量は物理ディスク量ではありません。取得不能は `Unavailable` で、SQL Serverの他接続・背景処理の混入も報告します。JSON・CSV・HTMLと分布・倍率を保存します。ネイティブ未定義の任意SQLは `NotImplemented` とし、成功した経路へ数えません。

CIはパッケージを先に作り、小規模の実クライアントfixtureをDebug／Releaseで検証します。ローカルSQL Serverの全DB実データ照合はCIの成功へ含めず、独立した受け入れ工程です。SSMSファイル形式検査と、実アプリでの読み込み・操作確認も別の検証結果です。

同じ稼働世代を再起動する場合は `pwsh ./TestSeamless/Invoke-Comparison.ps1 -Operation Restart` を使います。管理台帳・所有情報・検証証跡・パッケージ/モジュール/スキーマを確認し、記録済みPIDと生成時刻が一致する自分のサーバーだけを停止します。保存済みの同じEXEで再起動し、health成功後に新PIDを記録します。元RDBMSが停止・変更されても検証済みスナップショットの再起動は可能です。元RDBMSと直前の一覧/スキーマを再照合する新世代のActivateとは目的を分けています。サーバーは共有調停器へ依存せず、同じ8論理CPUの制限で直接起動します。起動や正常性確認の失敗は明示し、再取り込みで復旧成功に置き換えません。
