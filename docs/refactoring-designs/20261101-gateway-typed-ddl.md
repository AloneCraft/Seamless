# Gateway における型付きテーブルの生成・移行とトランザクション統合

## 背景

利用者が本計画の実装を明示承認した。SQL の CREATE / ALTER TABLE を、型付き C# テーブルの生成・ビルドと DynamicMethod による移行へ接続する。途中段階を計画全体の完成と報告しない。

## 現状の観察

- 新 profile の `GatewayDatabaseHost` は永続 manifest から型付き世代を開く。binding は世代の所有物とし、SQL / HTTP / Native の接続所有取引から同一 Gateway の複数 DB を共通確定へ参加させる。
- 新 profile の SQL CREATE / ALTER / DROP は生成・ビルド・型付き全表移行へ接続した。`RuntimeSqlCatalog` の固定 backing の CREATE / nullable ADD / DROP は従来 profile の動作であり、新方式の説明には使わない。
- `DatabaseContainer` と `TransactionScope` は外側の確定に参加するネストと保存点を持つ。保存点の変更集合もメモリ上限とディスク退避を利用する。
- SharedV2 は取引開始前の参加登録を要求する。`TransactionCoordinator` は独立した DB の確定を原子的に束ねない。
- `WalDraft` / `TransactionJournal` は metadata-only commit を持ち、結果照会と WAL 回収後の保持を扱う。prepared journal の有界チャンク・最終確定記録へ通常 DML と永続世代参照を結び付け、共通 checkpoint 後だけ WAL を回収する。
- 従来の `ComparisonHost` の読み込みは既定 ALC を使う。新 profile の生成 DB と登録済み Native module は、実際の DB アセンブリを共有する collectible ALC と世代所有 codec を使う。Native の再ビルド・保存点・共通公開・DTO formatter の参照解放を接続した。

## 過去設計から引き継ぐ仕様

`20261029-generic-sql/runtime-ddl.md` の原子的な確定・取消、未確定カタログの非公開、ID 非再利用、世代検査、メモリを超すデータの逐次処理、比較元の保護を維持する。

本計画の新プロファイルについて、固定 backing / 6 型 / nullable ADD のみという範囲と単一 DB 限定を置き換える。既存の保存形式を自動移行せず、旧方式を新方式として説明しない。既存ファイルの通常利用は維持する。

## 目的

- Gateway の SQL / HTTP / Native 経由で利用する。接続とサーバーは維持し、移行対象・依存 DB の実行を待機させる。無関係な DB は利用を継続する。
- 明示的取引、SQL Server 方式のネスト、保存点、同一 Gateway の複数 DB の一括確定・取消を実装する。
- CREATE / ALTER / DROP、型・NULL・長さ・精度、PK / UNIQUE / FK / CHECK / DEFAULT、IDENTITY・計算列と後続 DML を整合させる。
- DEFAULT の組み込み・式・T-SQL UDF、必要な汎用 UDF 実行と依存関係を扱う。
- C# クエリの自動再ビルド、collectible ALC、旧世代の安全な回収を実装する。

## 非目的

旧 Seamless 保存形式の引き継ぎ、別プロセスの保存領域直接利用、別サーバーとの分散取引、利用者 C# コードの意味を推測した自動修正は行わない。SQL Server からの取り込みは維持する。専用演算を未実装の型をバイナリ保存できることだけで完全互換と数えない。

## 提案設計

### 取引と保存点

接続が LogicalTransaction を所有し、取引 ID / ネスト数 / 状態 / 参加 DB / 行変更集合 / 保存点 / 非公開世代を保持する。AsyncLocal やスレッドだけを所有権にしない。

BEGIN は深さを増やし、内側 COMMIT は深さだけ減らす。最外側 COMMIT だけで公開する。保存点指定のない ROLLBACK は全体を取り消す。SAVE TRANSACTION と名前付き保存点への ROLLBACK、@@TRANCOUNT、XACT_STATE()、取引名を SQL Server と照合する。

保存点は有界・退避可能な行変更履歴と論理スキーマ世代を参照する。Gateway の表 overlay は初回保存点で変更集合を索引付きの退避領域へ移し、その後は更新前の索引位置を履歴へ記録する。保存点ごとに行の変更集合を複製しない。履歴のメモリ上限超過分をディスクへ退避し、復帰は既存索引の書換えとファイル縮小で行う。履歴復帰用バッファを更新前に確保し、復帰のために追加のメモリ・退避容量を要求しない。通常の単独 C# profile の保存点コピー方式は維持する。不可逆な列削除・型縮小を逆変換で取り消さない。必要な旧状態を保持する。エラー分類、XACT_ABORT、TRY/CATCH、取消、切断の挙動を分ける。

### 複数 DB と永続化

共通 WAL の一つの commit を唯一の確定判断にする。通常 DML の変更と、永続化・検証済みの候補世代への参照を同じ取引に含める。大量 DML は準備チャンクと最終 commit に分け、未確定チャンクは再生しない。DDL のみの commit も結果管理する。

SQL / HTTP / Native は同じ DB lease と世代を使い、遅延列挙の終了まで保持する。既存取引が完了できるように新規受付を制御する。既知 DB は一定順序で取得し、動的な依存追加による循環は検出して犠牲取引を取り消す。LOCK_TIMEOUT と取消を適用する。

復旧は全参加 DB の状態を整えてから受付する。確定不明なら該当 DB を停止し、無断で旧世代へ戻さない。全 DB のチェックポイントが確定情報を保持するまで WAL を削除しない。

### 型付き移行

永続 DB / 表 / 列 ID と SQL 型属性を持つカタログを使う。Type / delegate を永続化しない。CLR の cast を SQL 変換の仕様にしない。表現不能な値を黙って狭めない。

対象・依存 DB の取得 → 計画検証 → 型とキー生成・隔離ビルド → collectible ALC 読込 → 変換 delegate 生成 → DB 全体の逐次コピー → 行数・キー・制約・保存と成果物の検証 → 最外側 commit で公開、の順にする。未確定更新も移行に含める。PK がない表の内部行 ID と重複行を維持する。

MigrationPlan と RowConverterCompiler を分離する。変換は RowConverter<TOld,TNew>(in TOld,MigrationContext) 相当の DynamicMethod とする。単純経路では型付き列挙・投入と direct getter / constructor / 変換 helper を使い、行ごとの object[] と reflection invoke を避ける。複雑な UDF 評価の無割り当ては保証しない。

型対応表は整数・数値・文字列・バイナリ・日時・GUID・XML 等について、DDL / 保存 / 変換 / TDS を横断する。制約、WITH VALUES、DEFAULT 評価単位、非決定的な値、依存関係・SCHEMABINDING を SQL Server と比較する。評価済み値を保存し、再オープンで再評価しない。

### 世代・クエリ・回収

新 ALC は共通契約を共有し、旧 ALC への static 依存を持たせない。binding / codec / converter / SQL plan は世代の所有物とする。カタログ、HTTP、Native、SQL は同じ公開集合を見る。

Native 登録にソース・プロジェクト・依存 DB を追加して再ビルドする。失敗クエリは理由付きで無効化し、旧 DLL を新データへ実行しない。結果契約の変化をクライアントへ明示する。

成功した最外側 commit ごとに現行と直前 1 世代を保持する。保存点と読取が参照する候補は別に保持する。さらに古いコピーは回復情報を保護して回収する。データ保持と ALC の寿命を分離する。

## 影響範囲

Gateway の接続・取引・名前解決・カタログ・HTTP・Native、Query の DDL / 式 / UDF と世代検査、Rdbms の生成と metadata、Database / Transactions / Collections の変更履歴・参加・共通確定・checkpoint、関連テストに影響する。既存 standalone API は互換を保つ。

## 移行手順

新規の専用保存領域と fixture で検証する。元 SQL Server / 比較コピー / 稼働サーバーを変更しない。D に作業・実データ、F にキャッシュ・ログを配置し、Invoke-LocalProcess.ps1 と共通最大 8 論理 CPU を使う。既存 dirty ファイルを保護する。

## テスト計画

1. DML ネスト・保存点・状態・取消、複数 DB・一括 rollback・deadlock・非関連 DB 継続。
2. 新プロファイルと common WAL・準備チャンク・checkpoint、確定前後の強制終了・再起動。
3. DML / DDL 混在、複数 ALTER、不可逆変換後の保存点と全体 rollback、同一・別接続の可視性。
4. 全対応型の境界・制約・DEFAULT / UDF を実 SQL Server と値・metadata・取引状態で比較。
5. Native 再ビルド、ALC 解放、保持世代・参照中回収防止、SSMS / HTTP / Native の実利用。
6. メモリ上限・ディスク退避・容量不足・取消。C# / DynamicMethod / reflection の変換と総移行の速度・割当を測定する。

Debug / Release と package 利用試験、実 SSMS 画面を分けて記録する。未実行・部分実装を合格と数えない。

## リスクと対策

全 DB コピーの I/O と一時容量、明示取引中の長時間待機、外部コードの静的型依存、世代回収の誤り、複数 DB 確定の障害窓を優先して検証する。共有状態の更新と公開を同じ取引にし、途中成功で元データを壊さない。

## 未決事項

性能の絶対時間目標は設定されていない。機能ごとの実測と SQL Server 対応表を成果物とする。実装で契約変更が必要になった場合は本書と証跡を更新する。

## 合意状態

計画全体の実装は利用者承認済み。以下は進捗の記録であり、範囲の縮小ではない。

- [ ] 取引基盤
- [ ] 永続世代・共通確定
- [ ] 型付き DDL / DynamicMethod
- [ ] 型・制約・DEFAULT / UDF の広い対応
- [ ] Native 追従・回収・総合受け入れ

## 実装済みの構成要素と境界（2026-11-02）

### Gateway 共通確定の接続方針

既存の SharedV2 を独立に順次 COMMIT する方式は採用しない。新しい Gateway profile は共通 WAL ID と永続 DB ID をチェックポイントに保存し、通常の単独 COMMIT を拒否する。接続の論理取引が DB ごとの物理変更集合・保存点・lease を所有し、準備中は変更を公開しない。共通 commit の永続化後に全 DB の overlay を反映し、すべて終わってから lease を解放する。反映障害は参加 DB を障害停止し、共通 WAL から受付前に復旧する。

各 DB のチェックポイント LSN が異なる障害でも、共通 WAL を DB ごとの適用済み位置から再生する。共通 checkpoint は全 DB の使用権を取得し、共通 WAL の確定境界まで全 DB を保存してから確定する。その後だけ WAL prefix を回収する。新 profile は独立 SharedV2 WAL の存在や他の DB ID を拒否し、旧領域を暗黙に再解釈しない。

世代の準備では非公開 candidate の所有者を明示する。取引自身の更新を含む candidate を封印し、型付き codec でデータの内容を検証して保存する。この保存は共通 WAL の確定ではない。確定時の不変な `seed` と後続更新用の `live` を分け、DLL・カタログ・seed の hash を一つの世代 manifest に結び付ける。live の checkpoint は WAL ID・DB ID に加えて世代 ID も照合し、同一スキーマの別世代を取り違えない。領域の不足や欠損で旧 seed へ自動復帰しない。追加コピー・検証の I/O 時間は移行時間へ含めて報告する。

復旧で選択した世代の公開 commit LSN 以前は、その snapshot が含む更新である。旧スキーマの WAL を現在の codec へ渡す前に、この適用境界で除外する。世代選択・共通公開・保存点からの復帰への接続が完了するまでは、この保存機構だけで型付き DDL が完成したとは扱わない。

全段階は未完了である。SQL CREATE / ALTER / DROP から新しい型付き世代を自動構築・公開する経路、Native クエリの再ビルド・契約・寿命管理を接続した。無効・未信頼制約、広い列属性・型変換・UDF の全受け入れは残る。既存 runtime profile の DDL を型付き DDL の完成と数えない。

内部保存点への復帰や解放で資源・I/O障害が起きた場合は、取引を取消専用にする。文の取消が失敗した状態を、そのまま最外側 COMMIT できる状態として残さない。移行先のコンストラクターは列メンバー名と順序を大文字小文字も含めて検証し、同じ CLR 型の列の取り違えを型の一致だけで受理しない。

| 構成要素 | 実装と検証 | 残る接続・制約 |
| --- | --- | --- |
| SQL 取引 | ネスト、取引名、保存点、@@TRANCOUNT、smallint の XACT_STATE、文単位取消、XACT_ABORT と TRY/CATCH、取消専用のバッチ末尾3998。新 profile では複数 DB を接続所有の一つの取引へ参加させる | 全 SQL エラー分類、暗黙文内の状態照会、広い取消・切断の受け入れ |
| 変更集合 | Gateway は保存点を履歴位置で保持し、更新前の索引位置を有界・退避可能な履歴へ記録する。退避済み変更の復帰・再利用・解放、メモリと退避容量の残量ゼロでの復帰を検証した | 大量データ・取消・退避先 I/O 障害を含む総合受け入れ。通常の単独 C# profile は独立コピー方式を維持 |
| DB ロック | 取引 ID と DB ID の read/write/schema lease、取消、循環検出、犠牲取引の全体取消、無関係 DB の継続。SQL・HTTP・Native を同じ取得処理へ接続し、遅延列挙まで保持する | 既存取引と世代移行待機の統合、判明済み依存 DB の順序取得 |
| 準備 WAL | 有界チャンク、最終 commit、hash・範囲照合、metadata のみの結果管理、逐次 encode。新 profile の全 DB の DML を一つの commit で確定する。全 DB 保存後の共通 checkpoint と WAL 回収、DB 間で checkpoint が中断した後の復旧 | 世代の共通公開と成果物 hash、Gateway 全体を強制終了する障害注入。復旧索引は共有メモリ予算内で保持し、確定単位で WAL を再走査する |
| 生成と読込 | 隔離 C# ビルド、DLL hash、専用 collectible ALC、世代所有の canonical codec。永続 SQL カタログ・表/列 ID と hash 付き世代 manifest から再オープンする | SQL DDL からの接続、Native 成果物との一括公開と寿命管理 |
| 全表コピー | 型付き列挙・投入、DynamicMethod、列 ID 対応、未確定更新、列削除・追加、int→bigint、キーなし重複行と内部 ID、変更なし表、ディスク再オープン | 永続 ID の採番と DDL 差分からの計画作成、全 SQL 型変換規則、全制約・列属性・DEFAULT / UDF 検証 |
| 解放 | 通常生成と、新旧変換 delegate を使用した移行後の ALC 解放試験。取引と保存点が旧・非公開世代を保持し、復帰時は保持状態を選択する | 計画が保持する参照、Native 再ビルド、保持範囲を超えるデータ回収と ALC 寿命の総合検証 |

生成型の global MemoryPack formatter 登録は collectible profile で行わない。型を global cache に残す形式の serializer を避け、canonical typed store と世代所有 codec を使う。従来の profile は従来の serializer を使う。

SQL Server 比較は共有元 DB の表を変更せず、接続自身の一時表だけで実行した。ネスト・保存点の @@TRANCOUNT 維持、XACT_ABORT OFF の文単位取消、ON の CATCH 内の -1、バッチ末尾3998と全体取消、未捕捉の全体取消を確認した。取引状態の測定は読取文自身の取引に影響されるため、状態だけの SELECT と行数の SELECT を分けた。全エラー・型・メタデータ・制約の比較を完了した意味ではない。

追加比較では専用の global 一時表を二つの接続で使い、ロックタイムアウト1222と要求取消を確認した。XACT_ABORT OFF では状態1・ネスト1を維持し、ON では状態0・ネスト0となった。Gateway の実 SqlClient 接続で同じ4ケースを確認する。状態や定数だけの SELECT が既定 DB を不要に取得しないよう、計画は実際に参照した DB の世代だけを追跡する。

HTTP の通常要求は要求単位の接続所有であり、未完了の明示取引は要求終了で取り消す。要求をまたぐ利用には `/api/session` で発行する所有トークンを用い、活動中の取引 ID も各 SQL 要求で照合する。要求全体の直列化、明示終了、無操作期限と停止時の取消を扱う。SQL の出力は予算付き一時ファイルへ構築してから応答し、後続文のエラーで不正な JSON を返さない。Native は接続とクライアント取引 ID で所有し、通常エラーの文取消後は同じ取引を継続できる。旧 Native クライアントは新しい回復可能エラーフレームを受信した場合、安全に接続を終了する。

新 profile の作業・データは `D:/CodexTemp/gateway-v1/`、試験結果は `F:/CodexTemp/gateway-v1/`。従来の下表は前回の検証結果であり、以下の追加変更後の全体検証結果とは区別する。

`GatewayDatabaseHost` は新規領域の初期型付き DB 群を一つの commit にまとめ、再起動では checkpoint と WAL の manifest 参照から全 DB を選択してから受付を開く。SQL の CREATE／ALTER／DROP TABLE から生成・ビルド・全表変換へ接続し、複数回の ALTER、情報を失う変更後の保存点復帰、移行前後の未確定 DML、最外側だけの公開と再オープンを確認した。旧公開世代は、未確定 overlay を含まない直前の確定データを保存してから移行する。世代置換 DB と通常 DML の DB を同じ取引へ含め、ビルド・コピー・seal・WAL 準備／確定・反映・公開・checkpoint の 12 箇所で子プロセスを強制終了し、全 DB とスキーマが一括復旧する試験も成功した。

PK／UNIQUE／NO ACTION FK／CHECK／DEFAULT の追加削除を接続し、移行後・SQL／Native の文終了時・共通 commit 前に検証する。PK を外した表では内部行 ID へ移し、重複行を保持し、保存点でキーと元データを戻す。DEFAULT は式・組み込み日時・GUID・T-SQL scalar UDF を評価して保存し、nullable 列の WITH VALUES、後続 INSERT／UPDATE DEFAULT、関数と制約の取消を扱う。T-SQL scalar UDF は変数、分岐、WHILE／BREAK／CONTINUE、SELECT 変数代入、表参照、関数呼出、32 段までの再帰を共通評価器で実行する。副作用のある UDF は拒否する。関数オプションとスキーマ束縛の接続は検証中。

SQL Server 実機比較は比較元の業務 DB を変更せず、tempdb の専用一意名だけを作成して finally で破棄する。DEFAULT の非決定的評価単位、UDF ループ、依存 DEFAULT がある関数の ALTER 拒否 3729、保存点での関数復帰、禁止 NEWID の 443、datetime／smalldatetime／datetime2／time／datetimeoffset の丸めと境界を測定した。time は日末の丸めで翌日の 00:00 へ循環せず、その精度の最終時刻へ留まる。SQL 型全体・制約全体・エラー状態全体の比較が完了したという意味ではない。

collectible 行型の走査では `ArrayPool<T>.Shared` を使用せず、閉じた型が所有する通常のプールへ返す。.NET 10 で collectible thread-static の寿命・index 再利用に関する既知不具合が報告されており、この環境でも多数の世代を実行した後に CLR 0x80131506 の中断を再現した。世代所有プールへの変更後、同じ型付き世代・強制終了復旧の 28 件は完走した。Runtime 側の修正状況は [dotnet/runtime #134928](https://github.com/dotnet/runtime/issues/134928)、[修正 PR #134983](https://github.com/dotnet/runtime/pull/134983) を参照する。全 Gateway の Debug／Release 再検証と保持済み計画を含むアンロード確認は、この変更後の結果で判定する。

IDENTITY は seed／increment、自動採番、IDENTITY_INSERT、取消後・checkpoint／再起動後の非再利用、状態関数と RPC のスコープを接続した。Native の明示値も同じ採番位置を進め、IDENTITY 列の更新を拒否する。採番と新しい表／列 ID の予約は利用者の取引とは別の共通 WAL 記録で先に確定し、取引を取り消しても再利用しない。メモリの予約を WAL 書込前に行い、値の予約結果が不明な場合は対象 DB を停止する。rowversion／timestamp も DB 単位の永続カウンターを使い、SQL と Native の INSERT／UPDATE、追加列への既存行の値の生成、@@DBTS を扱う。取り込み済みの最大値は列を削除しても保持する。保存点への復帰では行の旧値が戻り、カウンターは戻さない。

算術・式による計算列は生成された型付き getter から世代の評価器を呼び、PERSISTED 値も文・取引確定前に保存値へ反映する。計算列の追加、保存点復帰、計算キー、非決定的な仮想列と PERSISTED 拒否を確認した。SQL と Native の同じ変更フックで FK の CASCADE／SET NULL／SET DEFAULT と親キー更新を扱い、複数の子表を含む取消を確認した。REFERENCES の列省略と自己参照、既存列の DEFAULT WITH VALUES が値を再評価しないことも検証した。

現時点では無効・未信頼制約、広い型の全変換規則、table-valued UDF を含む広い機能範囲の全受け入れは未完了。Native のソース登録、DDL に伴う再ビルド、コンパイル診断による無効化と復帰、引数・結果契約、DTO を含む ALC 解放、DB と Native の保存点・共通 WAL・checkpoint・回収は接続した。Native 定義だけの確定結果が不明な場合にも依存 DB を停止し、再起動で共通の確定結果へ戻す。Native・rowversion を含む 12 箇所の子プロセス強制終了から全 DB と query を復旧する Debug 試験も成功した。decimal／numeric の precision 29～38 は、CLR decimal へ黙って狭めず、拡張表現が必要である旨を変更前に拒否する。sql_variant の TDS・行バッファでは SqlDecimal の 38 桁を保持するが、型付き DDL の decimal 宣言の範囲とは区別する。段階のチェック欄は全受け入れを満たすまで未完了とする。

ソースへの ProjectReference を持たない専用 NuGet consumer で Debug / Release を検証した。生成・変換・未確定更新・ディスク再オープンに加え、複数 DB の共通確定、ネストした DDL、履歴保存点、Native のソース再ビルド・契約・復旧が成功した。最新の型・rowversion・共通予算の変更を含む版は別途再パッケージして確認する。実 SSMS 22.8 でも専用 DB で CREATE、ALTER、内側 COMMIT、保存点 ROLLBACK を操作し、データと列が戻る結果グリッドと正常終了を確認した。SSMS 表示の実行時間は 6.367 秒、8 行であり、上限の保証ではない。

10 万行、取引履歴のメモリ上限 64 KiB の専用 Release consumer では、全行更新と履歴退避、保存点への取消、２回の ALTER と情報を失う列削除からの復帰、公開 ALTER と checkpoint・再起動を確認した。保存点作成 6.13 ms、全行更新 8.72 秒、履歴取消 3.20 秒、共通 commit 8.31 秒、２回の移行と取消 79.17 秒、公開移行 124.18 秒。管理対象予算の最大値はメモリ 78,338,560 bytes、一時領域 103,300,928 bytes だった。SQL・Native の結果バッファも共通予算へ接続し、メモリと一時領域の同時枯渇で未確定変更を取消することを確認した。移行中の取消・メモリ枯渇・候補ディスク書込失敗でも、旧スキーマ・自身の未確定更新・無関係 DB の利用を保ち、host 解放後の管理対象予約がゼロになることを確認した。

20万行の2列単純変換の一回の Release 計測は、C# 直接 0.102 ms / 0 bytes、DynamicMethod 0.475 ms / 0 bytes、cached reflection 29.499 ms / 25,600,056 bytes。コンパイル時間・DB I/O・複雑な式・DEFAULT / UDF を含まない。方式全体の速度優位を保証する値ではない。

証跡は `F:/CodexTemp/gateway-v1/`、作業・実データは `D:/CodexTemp/gateway-v1/` に分ける。未コミット変更を保持し、Git HEAD 全体のクリーンビルドと同一とは扱わない。

| 検証対象 | Debug | Release |
| --- | --- | --- |
| Transactions（履歴位置の保存点・残容量ゼロでの復帰を含む） | 39成功 | 39成功 |
| Database.Transactions | 32成功 | 32成功 |
| Database.Query | GUID・binary・XML の追加後41成功 | 同じ41件成功 |
| Execution | 44成功 | 44成功 |
| Gateway 全体（Native 再ビルド・DTO 解放・契約・共通予算・rowversion・12 箇所の強制終了を含む） | DDL エラー状態と制約カタログを含む300成功 / スキップなし。後続の DML 式統合は関連回帰中 | 同じ300件で299成功・固定ポート競合1件。ポートをOS割り当てへ修正し、関連25件成功。後続変更を含む全体は再検証対象 |
| 世代回収（現在・直前・別DBの保持、取消候補の解放と回収後の再起動） | 再実行対象 | 1成功 |
| ProjectReferenceなしのNuGet consumer | 成功 | 成功 |
| 実 SSMS 22.8 の専用 SQL 操作 | 6.367秒・8行・正常終了。DDL、内側 COMMIT、保存点復帰を確認 | Release の画面試験とは区別 |

Gateway 全体は `gateway-all-debug-rowversion`、履歴位置の基盤は `transactions-all-history` と `transactions-release-history-02`、回収は `generation-collection-02`、10 万行の実測は `load-result.json` に結果を残す。回収対象は共通チェックポイントと WAL prefix 回収の完了後、全 DB の使用権を保持した時点で確定する。その集合だけを使用権解放後に削除するため、後から構築した世代を対象へ追加しない。DLL を保持する参照やファイルの排他が残る場合は診断付きで保留し、後のチェックポイントで再試行する。既存の NU1902 / NU1903（System.Security.Cryptography.Pkcs 9.0.13）は抑制していない。
