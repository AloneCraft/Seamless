# パッケージ分割・段階移行

## 移設先と依存方向（すべて目標、未作成）
| 既存資産 | 移行先・扱い |
|---|---|
| Execution/Database/Linq/FLinq/取引・storage | 現行を維持。Seamless.Coreは重複するため新設しない |
| Queryの共通catalog/型/diagnostics、独立IR | Seamless.Sql.Abstractions（BCLのみ） |
| Query実行・cache・安全なrewrite・CRUD共通化 | Seamless.Sql（Abstractions、Execution、Database） |
| ScriptDom解析・AST変換 | Seamless.SqlのOSS parser adapter（候補SqlParserCS）。ScriptDom adapterは移行中の比較用だけで最終配布に残さない |
| T-SQL型・関数・binding規則・sys/SSMS | Seamless.Compatibility.SqlServer（Abstractions、OSS parser adapter、Database metadata） |
| MySQL/PG意味論 | Seamless.Compatibility.MySql / PostgreSql、選定済parser adapterを利用 |
| Gateway TDS reader/writer/handshake | Seamless.Protocols.Tds（Sql service、SqlServer互換provider） |
| HTTP/nativeとhost組立 | Gatewayをcomposition rootとして維持。当初nativeの公開Clientを改名しない |
| Rdbms metadata/型生成、共通取り込み | Seamless.Migration.Core。SqlServer Bit判定・引用をproviderへ抽出 |
| SqlServer catalog/import | Seamless.Migration.SqlServer。MySqlConnector/Npgsqlのコネクターも各Migrationへ分離 |
| ADO.NET | Seamless.AdoNet（Sql service、必要provider）。TDSを必須依存にしない |
| GUI wire追加 | MySql/PostgreSql protocolはP5で新設。共有契約が確定するまでProtocols.Abstractionsを空で作らない |
| 比較基盤 | 既存TestSeamless/Benchmarkを再利用。Seamless.Benchmarksという出荷packageは需要・依存がない限り作らない |

Core/Database/Execution→Sql/Parser/Protocolは禁止。Abstractions→実装は禁止。Compatibility→wireは禁止。Migration→Gatewayは禁止。SQL serviceはinterface注入でparser/semantic providerを受け取り、concrete providerへ逆参照しない。ソリューション・NuGet graphの循環検査を必須にする。

## 移行順序
| 段階 | 変更とゲート |
|---|---|
| P0 | 現状・本仕様・評価・受け入れ。parser実測未完は解除条件付き保留 |
| P1a | parser依存syntax/enumを自前表現へ移す。既存SQL Server経路を移行用adapterで維持し、OSS側と独立期待値試験。最終ScriptDom除去の対象を台帳化 |
| P1b | typed IRと意味論profile、SELECT/CRUD共通binding、cache世代分離。既存bounded演算子へlowering |
| P1c | MySQL/PostgreSQLのparser adapter・binder・parameter・意味論providerを実装し、対象caseの構造と結果を合格させる。P3開始の必須条件 |
| P2 | ADO.NET、Factory、Dapper、async/取消/寿命。追加合意の最小制御文・実行時DDLは[DDL契約](runtime-ddl.md)の独立ゲートで実装し、既存Core/C12契約を使う |
| P3 | 3DB共通workloadとA〜Gの分離比較。正しさ通過だけ測定 |
| P4 | SQL Server移行分離を確定、MySQL/PG connector。Oracle/SQLiteの型・制約差分を設計 |
| P5 | TDS分離の受け入れとMySQL/PG wire、GUI実操作 |

P3のfixtureは公式providerで外部3DBへ構築し、Seamless側は型付きschemaを生成する。Migration connectorや追加wireの完成をP3の前提にしない。

TDS・SQL Server移行・nativeの回帰は全段階で行い、P5へ先送りしない。既存の19DB/171view比較計画は発展的・任意の工程として保持する。導入体験の必須受け入れとは分離し、未対応vendor機能の全実装を再要求しない。

## 公開互換性・復帰
公開型ごとに利用元、公開package版、保存関与、変更方針を台帳へ。未公開と確認したSQL APIは変更して利用側を再buildする。証拠不明は既存入口を残す。既存native API/MemoryPack契約/保存型/codec/WALを名称整理のため変更しない。必要な旧package facadeは個別に作り、全旧型を無条件に複製しない。

同一のimmutableデータコピーで旧/新経路を検証し、失敗時は旧入口と旧世代を維持する。比較のため同一保存先を複数processで開かない。未検証schema世代は公開しない。保存形式変更はこの移行に混ぜず、別仕様と明示backup/restoreを必須にする。

## 移行コネクター規範
Discover→権限/状態確認→consistent snapshot内catalog/rows→型付きschema生成/build→逐次取り込み→索引/制約→全値/NULL/重複/件数照合→世代公開。SQL元名とCLR名を別管理。DB一覧取得失敗・OFFLINEを削除と扱わない。型/照合/精度/timezoneが表現不能なら完全移行は停止。明示選択した部分移行は除外理由と依存破損を報告し、完全移行の成功にしない。

## 追加projectと作業管理
必要projectのみ作成し同じ変更でSeamless.slnxへ登録。既存focused-solution生成を更新。shared sourceはprojitems/shproj/SharedGUIDも同時更新。製品依存は同じ一意版でpack、利用側はPackageReferenceのみ、専用cacheとhashで出自を確認する。P0では製品project・参照・indexを変更しない。

最大3agent、ファイル所有を分け、source Xとstable build SをBGOで調停。C12の実際の実装/公開契約競合時だけ該当変更を止める。他者processを停止しない。DをSSD作業、FをHDD成果物に使いCへの新規書込を行わない。
