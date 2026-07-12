# fable5 一括改修 実装ノート (2026-07-02)

`20260702-fable5-seamless-database-flinq-benchmark-overhaul-prompt.md` に基づく実装の記録。
壊した API、追加した設計、検証結果、残作業をまとめる。

## 1. Seamless.Database 宣言 API + Source Generator

### 実行時 API (Seamless.Collections.Tsavorite/Database/, namespace Seamless.Database)

- `DatabaseContainer`: 宣言コンテナ基底。`this.Table<T>()` / `this.View(...)` / `this.TableFunction(...)` を
  CallerMemberName 単位でキャッシュ生成し、Dispose でストア/デバイスを解放する。
- `Table<TRecord>`: **Tsavorite `HashSet<TRecord>` の派生**。FLinq の型判定
  (`TryMatchGenericNamedType`) は基底型チェーンを辿るため、Table は自動的に Tsavorite ルートとして展開される。
- `DatabaseContainerOptions`: `StorageMode=Memory/Storage` と Index/Page/Segment/Memory サイズ。
  Storage は `StorageDirectory\{SqlName}\log|obj` のテーブル別デバイス。
- 属性 `[Database]` `[Table]` `[View]` `[Function]`: SQL Server 互換名の補助のみ。
- `IDatabaseImportSource`: 将来の AdventureWorks2008R2 本物バックアップ取り込み拡張点。
  外部 SQL Server・ネットワーク・本物バックアップには依存しない。

### Source Generator (Seamless.Database/SourceGenerator/GenerateContainer.cs)

`DatabaseContainer` 派生の partial クラスから生成:
- `DatabaseName` override([Database] があるとき)
- `DatabaseObjects`(Kind/MemberName/SqlName/RecordTypeName の静的メタデータ)
- `EnsureTablesCreated()`(全テーブル実体化 = スキーマ作成に相当する設計時操作)
- `ImportTables(IDatabaseImportSource)`(SQL 名で全テーブルへ行投入)

既存の Keys/Records partial struct 補完(DatabaseSchemaGenerator)は維持。

### FLinq 展開との整合(重要な設計制約)

FLinq は「System.Linq.Enumerable 呼び出しを含む**通常メソッド**の呼び出し箇所」を傍受する。
- 呼び出し箇所は `MethodDeclarationSyntax` 配下が必要 → プロパティ矢印本体のラムダ内 LINQ は展開されない。
- ラムダ内でも「LINQ を含むユーザーメソッドの呼び出し」なら展開される。

このため View/TableFunction の宣言形式は:
```csharp
public IEnumerable<R>XxxQuery(...)=>this.Table.Where(...)...;   // LINQ 本体(展開対象)
public View<R>Xxx(){return this.View(()=>this.XxxQuery());}     // メタデータ付きラッパー
```

### FLinq 側の一般化修正

`LinqFragmentBuilder.具象エニュメレータ型名取得` が `GetMembers`(宣言メンバーのみ)で
GetEnumerator を探していたため、`Table<T>:HashSet<T>` のような派生型で具象 struct 列挙子を
見つけられず CS0453 になっていた。基底型チェーンを辿るよう修正(派生コレクション全般に有効)。

## 2. テスト整理

- `Test/Seamless/Database/TPC_H.cs`: Container を宣言コンテナ化(8 テーブル + [Table] 属性 +
  View/TableFunction/スカラー関数 + Q01..Q22 維持)。Q テスト 44 件 + 新規 5 件すべて成功。
- `Test/Seamless/AdventureWorks2008R2/AdventureWorks2008R2.cs`: 旧スケルトン(約 90 個の宣言のみ構造体)を
  廃棄し、プロンプトの宣言 API イメージどおりの実働サブセット
  (Customers/SalesOrderHeaders/Stores/StateProvinces/CountryRegions +
  ActiveCustomers/StateProvinceCountryRegions ビュー + OrdersByCustomer テーブル関数 +
  CustomerTotalDue スカラー関数)へ再構築。名前空間は `Test.Seamless.AdventureWorks2008R2`。
  ベースラインは LINQ を使わない手続きコードで計算し、FLinq 展開結果と比較する。
  Storage モード・ImportTables・メタデータ検証テストを含む 7 件すべて成功。
- 全スキーマの本格移行は本物バックアップ取り込み時に `IDatabaseImportSource` 経由で行う。

## 3. SQLite (UiRunStore v3)

- **DDL/DML 分離**: DDL(テーブル+View 定義)は `EnsureSchema()` に集約し、ストアインスタンスごとに
  一度だけ実行。通常操作は DML/SELECT のみ。View の DROP+CREATE も EnsureSchema 内限定。
- **schema mismatch**: `UiRunSchemaMismatchPolicy.BackupAndRecreate`(開発、`runs.db.v{N}.{時刻}.bak` へ
  リネームして新規作成)/ `Throw`(通常運用、無警告破棄しない)。UI は DEBUG=Backup / RELEASE=Throw。
  旧 v1/v2 DB は SchemaMigrations から版数を読んで判定。既存 Work/UI/runs.db は初回起動時にバックアップされる。
- **ペア比較の修正**: Cases に `SourceTabId/ArgumentKey/Side/PairDiscriminator` 列を追加。
  保存時に BenchmarkSourceTabs の Left/Right 判定と Scalable 用型短名 discriminator を書き込む。
  `BenchmarkPairComparisons` は TypeFullName 完全一致をやめ、
  Side(Left/Right)+Method+ArgumentKey+Discriminator+N/Loop で対応付ける(0 行問題の解消)。
- **分析 View 13 個**: FlatResults / PairComparisons / TrendResults / ImprovementCandidates /
  TimeRatioWorst / AllocationRatioWorst / CategorySummary / HistoryTrend / ScaleTrend /
  MissingFailureMatrix / MeasurementQuality / FallbackDiagnostics / SelectedDetail。
  集計系は Completed Run のみ対象。CPU は MeasurementQuality の補助情報。
- **fallback 診断**: `FallbackDiagnostics` テーブル + `SaveFallbackDiagnostics/ListFallbackDiagnostics`。
- **汎用読み出し**: `QueryAnalysisView(viewName,limit)`(View 名ホワイトリスト、SELECT のみ)。

## 4. Benchmark UI

- **保存キュー**: `BenchmarkUiRunRepository` の Task.Run+Semaphore を直列タスクチェーンへ置換し、
  `FlushAsync()` で確定同期。既知の不安定テスト
  (QueueSaveResult_ReplacesMeasurementForSameJob)は 5 秒ポーリング待ちを廃止して安定化。
- **選択⇔キュー整合**: 選択セル辞書から実行対象を組み立てる規則を UI.Core の
  `BenchmarkSelectionQueueLogic` に抽出し、ViewModel(全タブ横断スナップショット)と共有。
  スナップショット⇔キュー一致・クロスタブ重複除去・表示中タブの生きた行優先をテストで固定。
- **右ペイン分析タブ**: 既存 5 タブに加え、SQLite 分析 View を表示する 9 タブ
  (改善ランキング/時間比/割当比/カテゴリ/履歴/スケール/失敗欠測/計測品質/fallback診断)を追加。
  表示は `BenchmarkAnalysisTextFormatter`(MAUI 非依存・テスト可能)による等幅テキスト表 + 更新ボタン。
- **改善プロンプト**: 合意どおり「段階分けせず一気に実装・テスト・検証まで」を明示する文面へ更新し、
  fallback カウンタ 0 維持の検証項目を追加。

## 5. fallback 0 検証

`20260702-fable5-flinq-enumerable-inventory.md` 参照。
- 実行時: `LinqCounter.EnumerableFallback(+Details)` を追加し、`GeneratedCodeHelpers.LinqInterceptorFailure`
  構築時に加算。
- 生成コード検査: `Test.SourceGenerator.FallbackDiagnostics.GeneratedInterceptors_ContainNoFallbackFailure` が
  `Test/obj/<Configuration>/net10.0/generated` の生成インターセプターに失敗例外が残っていないことを検証(現状 0 件)。
  Test.csproj に生成物出力のコンパイル前クリーン(stale 誤検出防止)を追加。

## 6. 壊した API / 破棄したもの

- `UiRunStore.Initialize()` → `EnsureSchema()` へ改名。`UpsertCase` は
  sourceTabId/argumentKey/side/pairDiscriminator を受け取る形へ拡張(旧引数は既定値で互換)。
  `UiCaseRecord` へ 4 プロパティ追加。スキーマ v3(旧 runs.db は自動バックアップ)。
- TPC-H Container の手書きストア構築(ctor/CreateTable/disposables)を廃止。
- AdventureWorks2008R2 の旧スケルトン構造体群(Schemas/Tables/Views/Functions 約 90 個)を削除。
  復元は git 履歴、または本物バックアップ取り込み時に生成し直す。
- `BenchmarkUiRunRepository` の Semaphore 保存キューを廃止(直列チェーン+FlushAsync)。

## 7. 検証結果 (2026-07-02)

- `dotnet build Seamless.slnx` 成功(0 警告 0 エラー)。
- `dotnet test Test/Test.csproj` 全 1166 件成功(2 分 4 秒)。
- MAUI `Benchmark/Benchmark.UI/UI.csproj` ビルド成功。

## 8. 残作業・既知の制約

- Enumerable 全メソッドの fallback 0 は「Test コンパイルに現れる呼び出し」で検証済み。
  呼び出し箇所レベルの設計除外(仮想メソッド・ジェネリック型メンバー・プロパティ矢印本体など)は
  インベントリ文書に理由を明記。プロパティ矢印本体対応は将来の LinqTargetDiscovery 拡張候補。
- `TryGetNonEnumeratedCount` の既知課題は未解消(既存挙動維持)。
- 分析タブはテキスト表形式。チャート化(SfCartesianChart)は次段。
- Fragments/Pipeline の不要分岐削除は、既存全分岐がテストで到達可能なため今回見送り
  (削除には分岐別カバレッジ計測が先)。
- 実行時 fallback カウンタの Run 保存(Worker→SaveFallbackDiagnostics 配線)は保存 API と View まで用意済み。
  Worker 側の計測フック接続が次段。
