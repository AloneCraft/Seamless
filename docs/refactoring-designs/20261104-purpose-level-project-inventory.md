# プロジェクトの目的別棚卸し

## 対象と確認方法

2026-11-04の作業ツリーを対象に、全csproj・shproj、master solution、参照、コンパイル条件、実行入口、共有ソース、外部環境依存を静的に確認した。調査用の設定一覧とSHA-256は`F:/CodexTemp/project-purpose-inventory-20261104-2015/inventory.json`に保存した。記載した削減数は受入検証を前提とする設計候補である。

今回の依頼は棚卸し。追加のプロジェクト削除は0件。ビルド・テストの再実行と移行後の保証同等性の受入は今回の調査に含めていない。

## 現在の構成

通常117、共有17の計134プロジェクト。テストプロジェクトは19件。直前の133件に加わった1件は`Seamless.Database.Client.Results`で、現在はmasterにも登録されている。実体とmasterの不一致は0件。

| 分類 | プロジェクト数 | 棚卸しでの扱い |
| --- | --- | --- |
| 自動テスト | 19 | 9つの保証目的へ整理する候補 |
| 比較実装をコンパイルする定義 | 34 | 6系統の定義に集約する設計候補。34の比較variantを維持 |
| 独立した実験用exe | 4 | workloadを既存CLIへ移す候補 |
| 薄いheadless CLI | 2 | 既存CLIのサブコマンドへ移す候補 |
| 共有ソースの可視化 | 17 | projitems所有・GUID・solution登録の規約を維持 |
| 製品、配布、サーバー、GUI、共通支援など | 58 | 公開契約・配置・依存方向を確認する境界。追加の非出荷ライブラリ候補3件を後述 |

## 上位目的で束ねる19テストプロジェクト

必要な保証を、結果・展開、資源と耐久性、DB契約、サービス境界、業務workload、計測、規模、サンプル業務、配布物受入に分ける。外部SQL・GUI・EGen・異常終了は、それぞれ明示した実行レーンと子プロセスを使う。

| 上位の保証目的 | 現在の所有プロジェクト | 統合後の候補 | 削減候補 |
| --- | --- | --- | --- |
| LINQの結果・例外・評価・展開・実画面 | Dynamic.Tests、Dynamic.UITests | Dynamic.TestsのCoreOnly／Windows／GUIレーン | 1 |
| 実行資源・コレクション・取引の耐久性 | Execution.Tests、Test.Persisted.Collections、Test.Tsavorite.Collections、Seamless.Transactions.Tests | 既存Execution.Testsを統合ホストへ拡張 | 3 |
| DBモデル・問い合わせ・取引・SQL連携 | Database.Compilation.Tests、Database.Query.Tests、Seamless.Database.Transactions.Tests、Database.Verification.Tests | 既存DBテスト1件を統合ホストにする | 3 |
| 接続・認証認可・公開・復旧 | Management.Tests、Seamless.Database.Gateway.Tests | Management.Testsを共通サービス検証ホストにする | 1 |
| TPCの業務整合性・取引・外部基準 | Benchmark.Tpc.Tests、Benchmark.TpcE.Tests、Benchmark.TpcE.Compliance.Tests | Benchmark.TpcE.Testsの通常／EGenレーン | 2 |
| 計測・履歴・監査 | Benchmark.Infrastructure.Tests | 現行の保証単位を維持 | 0 |
| 規模・容量・再構成 | Benchmark.Scalable.Tests | 現行の保証単位を維持 | 0 |
| サンプル業務シナリオ | Sample.NetShop.Tests | ソース版サンプルの受入 | 0 |
| 配布パッケージ・世代別モジュール | TestSeamless.Tests | 固定パッケージ版の受入 | 0 |
| 合計 | 19件 | 9件 | 10件 |

### 統合に必要な具体的条件

- DBの4件はnet10.0で、生成器参照・fixture・JSON/SQL資産を1つのホストへ移せる。SQL Serverレーンでは接続と実データの準備を必須にし、通常レーンと実接続レーンの実行記録を分ける。
- TPCの3件はnet10.0とInvariantGlobalizationを共有する。既存の非並列条件、EGenデータ・native harness、実際のSUT子プロセスを維持する。
- Transactions.Testsはexeで、`metadata-ack-stop`、`prepared-stop-preparation`、`prepared-stop-commit`、`commit-and-exit`の自己起動入口を持つ。統合ホストもexeとしてこれらをdispatchし、確定前後の強制終了と再オープンの保証を残す。
- Persisted／TsavoriteはBENCH_AS_TESTから継承される試験を含む。共有importを1回にそろえ、通常のFact・Theoryだけでなく継承された全データ行を照合する。既存の意図的な容量不足と、正常操作に必要な予約は別のfixture条件として維持する。
- ManagementとGatewayは、それぞれ異常終了・transportの自己起動を持つ。StartupObjectだけを差し替えると片側の検証が失われるため、両方の引数・終了コード・PID/生成時刻・affinityを検証する共通入口が必要。
- Dynamic.UITestsの1 Factは、指定したアプリと隔離デスクトップまたはAppiumへ接続する。Windows側に条件付きで移せる。CoreOnlyからUI用パッケージとソースを除き、GUIレーンはアプリ実体・7工程・保存結果を実行する。

### さらに2件を減らす案の扱い

InfrastructureとScalableの統合にはBENCH_AS_TESTのコンパイル単位と、同名の共有型を持つ比較DLLへの参照を整理する必要がある。先の10件とは別の設計候補とする。

NetShopとTestSeamlessは、前者が現在のProjectReference、後者が`$(SeamlessPackageVersion)`のPackageReferenceを使う。固定配布パッケージと世代別Schema／Queriesの受入を維持するため、9件案ではホストを分ける。同じcsprojのビルドモードへ集約する案は、両方の依存解決と実行レーンを保持することを条件とする。

## 比較用34プロジェクトの定義集約

共有ソースとコンパイル条件を調べた結果、34件は次の6系統に分かれる。

| 系統 | 現在 | 集約する定義 | 削減候補 |
| --- | --- | --- | --- |
| SystemのArray／List／IEnumerable／Range × FLinq／ZLinq／Ideal | 12 | 1 | 11 |
| 最適化All／Ideal／無効化4種 | 6 | 1 | 5 |
| PersistedのFLinq／IEnumerable FLinq／Linq／ValueLinq | 4 | 1 | 3 |
| TsavoriteのFLinq／IEnumerable FLinq／Linq／ValueLinq | 4 | 1 | 3 |
| ScalableのPersisted／Tsavorite | 2 | 1 | 1 |
| TPC-EのPersisted／Tsavorite／MVCC × FLinq／Linq | 6 | 1 | 5 |
| 合計 | 34 | 6 | 28 |

`SYSTEM_LIST`、`SYSTEM_IENUMERABLE`、`PERSISTED`、`TSAVORITE`、`DISABLE_REWRITE`などは実際のソース形状や経路を変える。集約案ではvariantごとに独立してコンパイルし、従来のAssemblyName・公開型・生成器設定・ベンチマーク一覧・計測条件を持つ出力を作る。比較variantは引き続き34件を検証する。

実装前に小さい1系統でMSBuildの概念実証を行う。同一プロジェクトパスの複数global propertyがrestore／project graph／GetTargetPathで正しく識別されること、variantごとのobj・bin・生成コードが分離されることを確認する。必要に応じて明示したbuild targetとvariant manifestから出力を解決する。

Worker、AuditのAuditedBenchmarkPaths、TypedBenchmarkAssemblyCatalog、GUIの行ID、Headless、focused solutions、CIを同じvariant manifestへ接続する。ソースを1回だけコンパイルして定数差を失う変更や、共通objへの上書きは保証を壊すため受入対象に含める。

この28件は構造変更を伴う見込み。単純に旧csprojを削除できる状態までの実装・測定同等性は未検証。

## 実験・CLIの6候補

| 現在 | 統合する目的と条件 | 削減候補 |
| --- | --- | --- |
| Benchmark.Scalable.Headless、Benchmark.Tsavorite.ParameterSweep.Headless | 既存net10.0 CLIのサブコマンド。要求JSON、終了コード、history／baseline／profile、取消、結果保存、毎回の子プロセス隔離を維持 | 2 |
| Experiments/GroupByLazyBench | Dynamicの明示的な実験workloadへ移す。int／参照型／32-byte struct、3キー型、入力規模、評価回数差とOptimize／TieredPGOを維持 | 1 |
| Experiments/HistoryStoreModes、TransactionModes、ProductTransactions | 既存の比較ホストへ実験モードを移す。実ストア、WAL、異常終了、snapshot、測定順・反復・資源予約・JSON形式を維持 | 3 |

実験4件はいずれも比較・再現用途のコードを持ち、ProductTransactionsなどは現在のスクリプトから利用されている。実験の結果・再現手順・入力manifestを引き継いだ後で旧入口を削除する。GroupByLazyBenchには製品ProjectReferenceがなく、手書きアルゴリズム比較の役割があるため、Dynamicの結果一致だけで代替したと判定しない。

Legacy WorkerとDynamic.Workerの統合も別候補。前者の既存アセンブリ実行プロトコルと後者の動的生成プロトコル、依存DLLの集合、冷起動とALC解放を保持する設計が必要であり、上の6件には加算していない。

## 非出荷ライブラリの追加候補

| 候補 | 移行先の候補 | 確認する保証 |
| --- | --- | --- |
| Benchmark.Dynamic.Infrastructure | Benchmark.Infrastructure | Dynamic.Coreの依存なしのモデル層を保ち、SQLite schema・resource名・保存済み履歴・Worker clientを維持。ローカルDBを変更する場合は明示バックアップと直接変更を行う |
| Benchmark.Tsavorite.ParameterSweep | Benchmark.Scalable.Core | 既にCoreから参照される容量探索機能を同じ目的へまとめ、予算・後処理・UI/CLI入口を維持 |
| Benchmark.TpcE.Core | Benchmark.Tpc.Core | 両者のTFMとMemoryPack／Hashing依存を確認済み。native layout、MemoryPack、assembly-qualified参照、friend、InvariantGlobalizationを照合する |

この3件も受入前の設計候補で、主要な削減数の合計には含めていない。

## テストの所有目的が重なっている箇所

`Seamless.Database.Transactions.Tests/DatabaseTransactionExpansionTests.GeneralRegression.cs`は、Dynamicが所有する`GroupByOrder.cs`と`LazyEvaluationCounts.cs`を再度埋め込み、Factメソッドを反射実行している。2ファイルには計29個のFact宣言がある。

この試験が追加で見ているのは、警告0、内部ValueTuple化、特定の生成形状と、追加したOrderingComparersであり、入力にDB取引モデルを含まない。生成器契約としてDynamicへ移す候補である。診断・形状・比較器の独自保証を残し、生成オプションの等価性を確認したうえで、同じ振る舞いソースの再実行を共通ランナーへ寄せられる。実行時fallbackとALC解放も共通の検証経路で確認する。

EGenの試験には、利用できない場合に通常returnする経路がある。EGenを担う統合後の必須レーンでは`TPCE_REQUIRE_EGEN=1`とデータ・native harnessの存在を要求する。空振りのPassedを外部整合性の保証として集計しない。

## 維持する境界

- 公開NuGet・製品サーバー・GUI・テンプレートは、配布と利用者の入口を担う。テスト所有先の整理から、これらの公開契約の廃止までは導けない。
- `Seamless/Seamless.csproj`には複数ランタイム・生成器を提供する配布入口の役割があり、ソース行数だけで空プロジェクトと扱わない。
- 新しいClient.ResultsはIsPackable=trueで、クライアント結果の予算・再生を提供する。テストの重複として削除する根拠は今回の調査から得られていない。
- 共有17件は、所有するprojitemsのソースをSolution Explorerへ表示する規約上の境界。ソース所有を整理せずshprojだけを削除すると規約を破る。

## 実施順と受入条件

1. 19テストの全ケース、Trait、環境条件、子プロセス引数、共有継承行をFに保存する。
2. DB4→1、TPC3→1を先行し、次にサービス2→1、Dynamic GUI2→1、実行・保存・取引4→1を実施する。新しいテストプロジェクトを増やす必要はない。
3. Debug／Releaseの通常、実SQL、GUI、EGen、異常終了を実行し、元ケースIDまたは明示した対応表で合格・実行環境を照合する。故意の検出器正例を除きfallbackを不合格にする。
4. 34比較variantのbuild定義を小さい系統から集約し、結果・例外・評価回数・生成形状・fallback・ABI・計測条件と独立出力を照合する。
5. 実験・CLI入口の受入後に、旧csproj、呼出し元、focused solutionsを整理する。

9テスト案で134→124、比較定義6系統案も成立すれば124→96、実験・CLIの6件も成立すれば96→90となる。各段階は設計候補であり、追加削除済みの件数は0件。3つの支援ライブラリ案、Worker統合案、さらに2つのテスト統合案はこの合計に加算していない。
