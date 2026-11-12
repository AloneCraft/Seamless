# Test廃止後のプロジェクトとテストの棚卸し

本書は残存5件の統合前の棚卸し記録。利用者の追加指示により、5件の統合・重複6ケースの整理・フォールバック確認の修正を実施した。現在の構成と受入結果は`20261104-test-project-consolidation.md`、操作方法と責務の正本は`../testing.md`に記載する。以下の138プロジェクト・24テストプロジェクトは統合前の値。

## 対象と確認方法

2026-11-04の作業ツリーを対象に、`Seamless.slnx`、全csproj・shproj・projitems、24個のテストプロジェクト、Dynamicのケース生成条件、移行対応表を静的照合した。比較対象のGit HEADは`bda4d77e`。

今回の棚卸しで追加した成果物は本文書。以下の追加統合候補については、ソース移動・プロジェクト削除前の判断を記録する。実行結果は先の移行受入記録を参照し、棚卸し時点の再実行結果とは区別する。

## 減ったプロジェクト数

今回のTest廃止による削減は**1プロジェクト**。`Test/Test.csproj`を削除し、新しいテストプロジェクトの追加は0。

元の追跡ファイル598件のうち595ソースを既存の所有先へ移し、旧プロジェクト固有のcsproj・global using・assembly設定の3ファイルを廃止した。移行前2250ケースの欠落0・未検証0は`20261104-test-coverage.json`に記録されている。

| 集計対象 | HEAD | 現在 | 差分 |
| --- | --- | --- | --- |
| csproj | 93 | 121 | +28 |
| shproj | 16 | 17 | +1 |
| 合計 | 109 | 138 | +29 |

ソリューション差分は追加30件・削除1件。追加にはDB・Management・テンプレート・実験用プロジェクトなどが含まれる。Test移行単独の削減と、作業ツリー全体の増減は集計範囲が異なる。

全121 csprojと17 shprojはmaster solutionに登録済み。登録先ファイル欠落0、登録外プロジェクト0。全17組のprojitems・shprojについてGUIDとShared importの対応に不整合0。

## 追加で入口を統合できる候補

以下は既存テストを移してプロジェクトを減らす候補。記載したメソッド数は通常のFact・Theoryの宣言数で、Theory行数や共有ソース由来の検出ケース数とは異なる。

| 優先度 | 候補 | 現在の保証 | 統合先の候補と条件 |
| --- | --- | --- | --- |
| 優先 | `Benchmark.Headless.Tests` | 1ファイル・2メソッド。複数実装計測で理想アセンブリ名と型名を解決する規則 | `Benchmark.Infrastructure.Tests`のWindows側。Headless参照とInternalsVisibleToを移し、net10.0側へのWindows依存流入を防ぐ |
| 優先 | `Test.Tsavorite.ParameterSweep` | 2ファイル・15メソッド。スイープ計画、設定、実行、保存層 | `Benchmark.Infrastructure.Tests`のnet10.0側。ParameterSweep本体参照と実ストアの後処理を維持する |
| 条件付き | `Test.Scalable.Persisted.Collections` | 独自2メソッドに加え、共有ソースの継承されたBenchTheory | `Benchmark.Scalable.Tests`。BENCH_AS_TESTで発見されるケース一覧を移行前後で照合する |
| 条件付き | `Test.Scalable.Tsavorite.Collections` | 独自3メソッドの設定・スレッド別所有・Disposeと、共有ソースのBenchTheory | `Benchmark.Scalable.Tests`。共有型・Commonの重複コンパイルを解消し、BENCH_AS_TESTと所有権検査を維持する |
| 条件付き | `Benchmark.Audit.Tests` | 3テストファイル・8メソッド。IL監査、理想版との対応、測定分類 | `Benchmark.Infrastructure.Tests`。同名型の衝突を避けるビルド参照と、監査対象DLLの明示的な取得を維持する。現在のAudit fixtureはrepo内binを探索するため、Fの隔離成果物への対応も必要 |

優先候補2件を統合すると、現在の24テストプロジェクトは22件になる。条件付き3件も検証を終えて統合できれば19件になる。5件は削減見込みであり、追加削除の実績は0件。

## 重複実行を整理できるテスト

### 同じ生成条件で実行している6ケース

`DynamicFixedDefectTests.要素型の次元で見つけて直した不具合は直ったまま`の6行は、`DynamicElementTypeTests.要素型ごとのランダムなチェーンが全経路で一致する`の範囲に含まれる。

| 要素型 | QueryIndex |
| --- | --- |
| CollidingKey | 0 |
| Int256Struct | 27 |
| Order | 27 |
| Order | 0 |
| String | 0 |
| String | 2 |

両方ともseedは`0xE1E7`、SystemArray、入力16件、演算子数3、深さ3、binary・nested有効、同じ能力表を使う。広域側は各型でindex 0〜29を実行する。Workerのfixture seedもBaseSeedとQueryIndexから決まる。run IDと測定profileの名称には差があるが、測定profileの数値設定は同じ。

固定回帰の識別情報を広域側のケース表に持たせ、同じケースの実行を1回へまとめられる。これにより6回の重複実行を減らせる。テストプロジェクト数は変わらない。

### 目的と実行頻度を確認して扱う重なり

- 124件の既知修正回帰と、通常の2中間演算子チェーン抽出は、夜間の全組み合わせにも含まれる。通常CIで短時間に既知不具合を拾う保証と、夜間の網羅保証を分けている。同じジョブで全網羅を実行する場合は、ケースID単位で実行をまとめる余地がある。
- `FixedZLinqCoverageTests`は固定30件を、境界値テスト内でも使うZLinq経路で実行する。入力と第2ソースが異なり、ZLinq呼出先の存在も明示確認する。固定ケース共通ランナーへ入力行と必須実装の検査を移してから、独立した入口を整理できる。
- `DynamicEvaluationCountSurvey`はEVAL_SURVEY未指定時に即returnする調査用Fact。通常の回帰保証には寄与しない。調査機能の維持先を決めれば、テスト発見対象から分離できる。

## 別の保証として維持する境界

- `LegacyChainCatalog`は原式の引数と225件の保存コーパスを実行する。`DynamicCorpusRegressionTests`はIRに変換できる144件を実行し、キー射影やSelectの式が原式と異なるものを含む。両者には生成経路と入力の差がある。
- 移行した1206振る舞いケースは、独自型、オーバーロード、例外、複数providerなどを含む元ソースのassertionを使う。チェーンの演算子名が同じことだけでは等価性を確定できない。
- `StaticBuildContracts`の3行は、実MSBuildでのpartial・ジェネリック・独自型とinterceptor接続を確認する。Dynamic内の少数ケースへ集約済み。
- 生成器の契約376行は、診断、生成形状、incremental cache、analyzer、低レベルのランタイム構造を含む。通常の結果比較とは観測対象が異なる。
- フォールバック検出器の正負例、例外・入れ子・Resetの試験は、検出器が壊れて全ケースを誤合格にすることを防ぐ。
- GUI実画面、DBの生成・SQL接続・転送、トランザクション、実ストアの復旧・所有権、TPC-Eの整合性・redo・強制終了は、それぞれ外部境界の保証を持つ。
- FLinq・ZLinq・Ideal・最適化の設定別benchmarkアセンブリは、共有ソースと異なるコンパイル条件・実装を比較するために使う。Dynamicへの機能回帰集約だけでは、その計測・監査用途の廃止を確定できない。

## 統合時にそろえる観測点

`FixedCaseCatalog.PreservesBoundaryResultsAndExceptions`と`LegacyChainCatalog.Execute`は、LoadedDynamicQueryを直接実行し、結果または例外型を比較している。生成時のFLINQ002・失敗マーカー検査はDynamicQueryCompilerに存在するが、この2経路は実行時FallbackCountを直接assertしていない。

共通ランナーへ整理する際は、例外を捕捉する内側にも`DynamicFallbackCapture`を置き、結果・例外比較と同時にFallbackCount 0を確認する。ここで記録したのは観測点の差であり、フォールバックの実発生を再現した結果ではない。

## 全24テストプロジェクトの分類

| プロジェクト | 担当する保証または整理先 |
| --- | --- |
| Benchmark.Dynamic.Tests | チェーン・元ソース回帰・生成器契約・Worker・履歴・画面モデル。内部の重複実行を整理 |
| Benchmark.Dynamic.UITests | 実GUI・UI Automation・実行結果保存 |
| Benchmark.Infrastructure.Tests | 計測・履歴・UI共通部品。Headless・ParameterSweep・Auditの移行先候補 |
| Benchmark.Headless.Tests | Infrastructureへの統合優先候補 |
| Benchmark.Audit.Tests | Infrastructureへの条件付き統合候補 |
| Benchmark.Scalable.Tests | スケール計画・runner・設定。Scalable Collectionsの移行先候補 |
| Test.Scalable.Persisted.Collections | Scalableへの条件付き統合候補 |
| Test.Scalable.Tsavorite.Collections | Scalableへの条件付き統合候補 |
| Test.Tsavorite.ParameterSweep | Infrastructureへの統合優先候補 |
| Benchmark.Tpc.Tests | 共通乱数・正規化・redo log形式と書込み |
| Benchmark.TpcE.Tests | TPC-Eの実取引・比較実装・プロセス境界 |
| Benchmark.TpcE.Compliance.Tests | EGen・整合性・監査レポート・外部ハーネス |
| Execution.Tests | 実行基盤・Store設定・所有権 |
| Test.Persisted.Collections | Persisted実ストアと共有コレクション試験 |
| Test.Tsavorite.Collections | Tsavorite実ストアと共有コレクション試験 |
| Database.Compilation.Tests | DBスキーマ・生成・コンパイル |
| Database.Query.Tests | DBクエリ・実行意味 |
| Database.Verification.Tests | SQL Server接続・転送・生成CLI |
| Seamless.Database.Gateway.Tests | DB gateway・通信とsession境界 |
| Seamless.Database.Transactions.Tests | DB上のトランザクション |
| Seamless.Transactions.Tests | トランザクション基盤 |
| Management.Tests | 管理API・認証・管理画面モデル |
| Sample.NetShop.Tests | サンプルの業務シナリオ |
| TestSeamless.Tests | サンプルサーバー・クライアントの統合動作 |

## 検証範囲

- master solutionと実ファイルの対応、共有プロジェクトのGUID・importを照合した。
- 通常のFact・Theoryを含むファイルの内容を正規化して照合し、ファイル全体が同一の重複は0件だった。コメント・空白を除いた一定長以上のpublicテストメソッド本体の抽出でも、同一トークン列は検出されなかった。これらの静的照合は意味的な重複の不存在を証明しない。
- 追加統合候補は、依存参照、対象framework、共有ソースのimport、friend assembly、実行コードを確認した。移行後のビルド・検出ケース照合・実行による受入は各候補の実装時に行う。
- 元2250ケースの受入根拠は`20261104-test-coverage.json`と`20261104-test-verification.json`。本棚卸しでは全テストの再実行を行っていない。
