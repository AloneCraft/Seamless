# fable5 完全版一括実装プロンプト

この文書は、`G:\GitHub\Seamless` を fable5 に渡して一気に実装・リファクタリングしてもらうための完全版プロンプトである。

この版は、単なる要件だけでなく、このチャットで合意した判断、コード調査で分かった現状、過去チャットや git 履歴から見えた試行錯誤の長い領域、既知の失敗、DB/UI/テスト上の注意点を含める。

## fable5 にそのまま貼るプロンプト

```text
あなたは fable5 として、G:\GitHub\Seamless の大規模一括改修を担当してください。
回答・説明・作業ログ・最終報告は必ず日本語で書いてください。

最初に必ず読むもの:
- G:\AI\ai_rules.md
- G:\GitHub\Seamless\AGENTS.md
- G:\GitHub\Seamless\AI_SYNTAX_RULES.md
- G:\AI\Skills\plan-model-gate\SKILL.md
- 作業内容に該当すると判断した G:\AI\Skills 配下の SKILL.md

開始点:
- ユーザーは直前までの作業をコミット済みです。
- 開始基準は現在の HEAD、最新コミット edcbaa62「スピナーと進捗ゲージを分離した」です。
- master は origin/master より ahead 32 の状態でした。
- 既存未コミット変更があればユーザー作業として扱い、勝手に破棄しないでください。
- このプロンプトファイル自体は docs/refactoring-designs にある指示文書です。必要なら参照し、実装対象コードとは分けて扱ってください。

大方針:
- Seamless ソリューション全体を対象に、無関係に見えるコードも含めてリファクタリングしてよいです。
- 公開 API 互換や既存テストの呼び出し形を壊してもよいです。
- 壊した場合は新 API、新テスト、新ドキュメントへ一貫して更新してください。
- できるだけ短く、簡潔で、美しい構造へ整理してください。
- ただし動作保証、計測再現性、DB保存、Source Generator の正しさ、FLinq 展開、テスト網を優先してください。
- 文字化けしているコメント、識別子、UI文言、テスト名、ドキュメントは、意味を推定できる範囲で正常な日本語または英語へ修復してください。

合意済みユーザー判断:
- 出力はチャット貼り付け用プロンプトと、リポジトリ内 Markdown 保存の両方。
- 全面リファクタリング対象はソリューション全体。
- 既存 API 互換は維持しなくてよい。
- AdventureWorks2008R2 は今は外部 SQL Server 接続なし。本物バックアップは後でユーザーがダウンロードする。
- 今はリポジトリ内のコード上の情報だけで実装する。
- yield return 禁止は、生成される実行時ライブラリと手書き実行時ライブラリの列挙処理だけに適用する。
- Source Generator 内部やテスト補助の小さな設計時列挙処理は yield return 禁止対象外。
- System.Linq.Enumerable 全メソッドを対象に fallback 0 を目指す。
- fallback 検証は生成コード検査と実行時診断ログ/カウンタの両方で行う。
- Seamless.Database の宣言は普通の C# を主、属性を SQL 名などの補助にする。
- Table<T> は Tsavorite/Persisted 系を使う。
- Table<T> はパラメータでメモリ範囲内運用とストレージ利用を切り替えられるようにする。
- Benchmark UI は SQLite 正本の測定結果表示を前提に、選択状態と実行キュー生成の整合を直す。
- 既存 runs.db は捨ててよい。
- schema mismatch 時、開発用では既存DBをバックアップリネームして新規作成してよい。
- Release/通常運用で無警告にDBを破棄しない。
- DDL は設計時/スキーマ作成/更新時だけ。通常実行時は DML/SELECT のみ。
- CPU 型や CPU 差は改善対象ではない。必要なら計測品質の補助情報に留める。
- 分析は Completed Run の履歴集計を基本にする。Running/Stopped は原則除外。
- 比較方向は既存 BenchmarkSourceTabs の Left/Right を使う。Left が Right に負けるほど改善候補上位。
- 改善プロンプトは段階分けせず、選択候補を一気に実装まで依頼する内容にする。

現状調査メモ:
- Work/UI/runs.db が存在します。
- 既存DBテーブルは Apps、Runs、RunStates、Cases、CaseArguments、Jobs、Measurements、MetricValues、Comparisons、UiSelections、UiViewStates、SchemaMigrations などです。
- 既存Viewは BenchmarkFlatResults、BenchmarkPairComparisons、BenchmarkTrendResults です。
- 調査時点の既存DB件数は Runs 47、Cases 299、Jobs 2209、Measurements 2207、MetricValues 5540、Comparisons 0、UiSelections 2018、UiViewStates 132 でした。
- MetricValues は AllocatedBytes、ErrorNanoseconds、MeanNanoseconds、StdDevNanoseconds が中心です。
- BenchmarkPairComparisons は 0 行でした。原因は TypeFullName 完全一致で Left/Right を比較しているためです。
- 実データは Left/Right で Benchmark.System.Linq.Enumerable.FLinq と Benchmark.System.Linq.Enumerable.ZLinq のように型名が違います。
- UI側の BenchmarkComparisonFormatter は通常タブでは method + argumentKey、Scalable/重複時だけ type discriminator を使うため、DB View と不一致です。
- UiRunStore.Initialize() は現状 DDL と通常読み書きの境界が曖昧です。
- 既存 CreateAnalysisViews は CREATE VIEW IF NOT EXISTS のため、View定義変更が反映されにくいです。
- ただし通常実行時に DROP VIEW / CREATE VIEW を繰り返す設計にはしないでください。
- dotnet build Seamless.slnx -v:minimal は過去調査で成功しています。
- dotnet test Test\Test.csproj -v:minimal --no-build は 120 秒でタイムアウトしました。
- dotnet test Test\Test.csproj -v:minimal --no-build --filter FullyQualifiedName~Test.Benchmark は 141 件中 1 件失敗しました。
- 失敗テストは Test.Benchmark.BenchmarkUiRunRepository.QueueSaveResult_ReplacesMeasurementForSameJob です。
- 原因候補は非同期保存キューで、5秒待ちでも2回目保存反映が間に合わない不安定さです。

既存UI構造メモ:
- BenchmarkTab は Cpu、Chart です。
- BenchmarkLowerTab は Cpu、Log、ImprovementPrompt です。
- CreateBenchmarkTabContent() は左に benchmark grid、右に chart panel、下段に lower tab view を持ちます。
- 右ペインの CreateBenchmarkChartPanel() には既に SfTabView があります。
- 既存右ペインタブは 比較チャート、遅延ホットスポット、メモリ/割当、CPU/タイムライン、生成コード監査 です。
- 右ペイン既存チャートタブ領域を拡張するのが自然です。
- 下段 ImprovementPrompt は候補選択と fable5 実装プロンプト生成/コピーに使ってください。
- 保存済みRun表示は SQLite から読み出しています。
- SourceTab/ページ切替時は currentRunSession のメモリ上結果を優先し、なければ選択RunのDB状態を使う二段構えです。
- 測定結果はSQLite正本でよいですが、BenchmarkCell/BenchmarkRow/ImprovementCandidate の選択状態、実行開始時スナップショット、実行キュー生成、Run/SourceTab/ページ切替後の復元が一貫しているかを直してください。

過去の試行錯誤が長い領域:
- Benchmark UI、SQLite保存、Scalable/LoopCount、FLinq/Persisted/Tsavorite、Tsavorite IterateLookup/ScanCursor、UI再構築、テスト不安定、文字化け、共有プロジェクト構成。
- 共有プロジェクト構成と計測UI改善では LoopCount軸、SQLite分析ビュー、右/左ペア、チャートCompletedペアのみ、共有プロジェクト移動、テスト成功履歴がありました。
- UI再構築では DB正本化、CSV廃止方針、Benchmark完全DB正本化が未完了でした。
- 分析候補検討では保存キューテスト不安定、Fast worker 並列、Console.SetOut 共有状態などの懸念がありました。
- Persisted/Tsavorite性能改善では Tsavorite ScanCursor vs IterateLookup、provider-native terminal、foreach/enumerator、tombstone/chunk境界、FLinq fragment coverage が難所でした。
- テストエラー調査では ResultWriter 同時書き込み、Tsavorite Dispose checkpoint キャンセル、Scalable metadata/threshold stop などがありました。
- CPUベンチ名と停止原因では、CPU型そのものよりベンチ測定条件と停止理由の扱いが問題でした。

1. Seamless.Database の再設計と実装:
- Seamless.Database の Source Generator は、ユーザーが定義した宣言的コードから、データベースを構築する partial コードを生成するものにしてください。
- Container、Table、View、scalar function、table function を宣言できるようにしてください。
- 宣言の主形式は普通の C# 型/プロパティ/メソッドにしてください。
- 属性は SQL Server 由来の schema/table/view/function 名など既存 DB 互換を補う補助として使ってください。
- 例: partial Container が Table<T>、View<T>、Function、TableFunction を持ち、View/Function/TableFunction の中身は Container の Table/View に対する通常の LINQ として書ける形にしてください。
- ユーザー定義の宣言的関数・テーブル関数は、普通に Container の Table や View を LINQ で書くだけで、FLinq にインターセプトされるようにしてください。
- Table<T> の既定実体は Tsavorite/Persisted 系にしてください。
- コンテナ作成オプションで、メモリ範囲内運用とストレージ利用を切り替えられるようにしてください。
- 今は外部 SQL Server、ネットワーク、本物バックアップファイルには依存しないでください。
- 後で AdventureWorks2008R2 の本物バックアップを取り込める拡張点だけ残してください。
- Test\Seamless\AdventureWorks2008R2\AdventureWorks2008R2.cs と Test\Seamless\Database 以下の既存コードを、Database として使える宣言的コードへ整理してください。
- Test.Seamless.AdventureWorks2008R2 は現状プロジェクト名ではなく Test\Seamless\AdventureWorks2008R2\AdventureWorks2008R2.cs として存在します。既存配置に合わせて必要ならテストプロジェクト/名前空間を整理してください。
- AdventureWorks2008R2/TPC-H 的な Container/Table/View/Function/TableFunction 呼び出しが、FLinq で展開され、期待結果と一致することをテストしてください。

宣言APIイメージ:
```csharp
public sealed partial class AdventureWorks:DatabaseContainer{
    public Table<Customer>Customers=>this.Table<Customer>();
    public Table<SalesOrderHeader>SalesOrderHeaders=>this.Table<SalesOrderHeader>();

    public View<ActiveCustomer>ActiveCustomers=>this.View(()=>
        this.Customers
            .Where(customer=>customer.StoreId.HasValue)
            .Select(customer=>new ActiveCustomer(customer.CustomerId,customer.StoreId!.Value)));

    public TableFunction<SalesOrderSummary>OrdersByCustomer(int customerId)=>this.TableFunction(()=>
        this.SalesOrderHeaders
            .Where(order=>order.CustomerId==customerId)
            .Select(order=>new SalesOrderSummary(order.SalesOrderId,order.TotalDue)));

    public decimal CustomerTotalDue(int customerId)=>
        this.SalesOrderHeaders
            .Where(order=>order.CustomerId==customerId)
            .Sum(order=>order.TotalDue);
}

[Database("AdventureWorks2008R2")]
public sealed partial class AdventureWorks{
    [Table("Sales.Customer")]
    public Table<Customer>Customers{get;}

    [View("Sales.vActiveCustomer")]
    public IQueryable<ActiveCustomer>ActiveCustomers()=>
        this.Customers
            .Where(customer=>customer.StoreId.HasValue)
            .Select(customer=>new ActiveCustomer(customer.CustomerId,customer.StoreId!.Value));
}
```

2. FLinq / Fragments / Pipeline の完全展開:
- .NET の System.Linq.Enumerable 全メソッドを対象に、未対応なら展開実装を追加し、fallback 0 を目指してください。
- 「展開」とは、フォールバックすることなく LINQ メソッドが生成コードへ展開されることです。
- 生成コード検査と実行時診断ログ/カウンタの両方で、fallback 未発生を検証できる仕組みを入れてください。
- 理論的・意味論的に展開できないものだけを設計上の除外にしてよいです。
- 除外する場合は理由をコードコメントまたは docs に明記し、テストで除外理由が分かるようにしてください。
- Fragments と Pipeline は、過去に AI の試行錯誤で増えた不要物が含まれる可能性があります。
- 全分岐ルートを棚卸しし、理論的に到達不能・重複・責務不明なものは削除してよいです。
- 削除理由を残してください。
- Fragments と Pipeline のすべての分岐ルートを通るテストを追加・整理してください。
- 手書き実行時ライブラリと生成される実行時列挙コードでは yield return を使わず、値型の IEnumerator 継承型または同等の値型 enumerator を使ってください。
- Source Generator 内部やテスト補助の小さな設計時列挙処理は yield return 禁止対象外です。
- Tsavorite/Persisted/FLinq の改善では、ScanCursor / IterateLookup、chunk 境界、tombstone、provider-native terminal、delegate fallback、匿名型 carrier、correlated subquery lookup 化など、過去の難所を優先して確認してください。
- 既存 docs/refactoring-designs/20260624-1806-flinq-intercept-optimization-pipeline-fragment-spec.md を参照し、古い設計が現実と合わない場合は実装に合わせて更新してください。

現状存在する主な Fragments/テスト観測:
- Fragments には Aggregate、AggregateBy、All、Any、Append、AsEnumerable、Average、Cast、Chunk、Concat、Contains、Count、CountBy、DefaultIfEmpty、Distinct、DistinctBy、ElementAt、Except、First、GroupBy、GroupJoin、Index、Intersect、Join、Last、LeftJoin、LongCount、Max/Min、OfType、OrderBy、Pipeline、Prepend、Reverse、RightJoin、Select、SelectMany、SequenceEqual、Shuffle、Single、Skip、Sum、Take、ThenBy、ToArray、ToDictionary、ToHashSet、ToList、ToLookup、Union、Where、Zip、Tsavorite/Persisted root/terminal などがあります。
- FragmentCoverage/Chain1/Chain2 テストは広く存在しますが、AsQueryable、Empty、Range、Repeat、OrderDescending 系など、Fragment一覧との対応が曖昧なものがあります。
- まず現状一覧を機械的に棚卸しし、未展開、fallback、冗長Fragment、到達不能分岐を潰してください。

3. Benchmark UI / SQLite / 分析チャート:
- UI のベンチマークは、チェックを入れたベンチマークをすべて実行するように直してください。
- 現在は全部実行されない、またはページ/SourceTab/Run 切替で復元されない疑いがあります。
- 測定結果は SQLite 正本として扱ってよいです。
- ただし選択状態と実行キュー生成がページ切替、Run切替、SourceTab切替で一貫するように修正してください。
- 現状コードでは保存済みRun表示は SQLite から読んでいます。
- 一方で SourceTab 切替では currentRunSession のメモリ結果を優先し、なければ選択RunのDB状態を使っています。
- この二段構えを壊さず、チェック状態/実行対象/表示復元の整合を取ってください。
- UiSelections / UiViewStates / ProgressState / BenchmarkCell / BenchmarkRow / ImprovementCandidate の保存復元を見直してください。
- 実行開始時の選択スナップショットとキュー生成が一致するテストを追加してください。
- 右ペインの既存チャートタブ領域を拡張し、ユーザーが改善対象を見つけやすい分析チャート/表を 8-12 個程度作ってください。
- 下段 ImprovementPrompt タブは候補選択と fable5 実装プロンプト生成/コピーに使ってください。
- 分析対象は Completed Run の履歴集計です。
- Running/Stopped は原則集計対象外にしてください。
- 比較方向は既存 BenchmarkSourceTabs の Left/Right を使ってください。
- Left が Right に負けるほど改善候補として上位に出してください。
- CPU型・CPU差は改善対象ではありません。必要なら計測品質の補助情報に留めてください。
- 生成する fable5 向け改善プロンプトは、選択候補を段階分けせず一気に実装まで依頼する内容にしてください。

右ペイン分析タブ案:
- 改善ランキング: 総合スコア順の表。
- 時間比: Left/Right の時間比ワースト。
- 割当比: Left/Right の割当比ワースト。
- カテゴリ: Aggregate / Set / Projection などカテゴリ別集計。
- 履歴: Runごとの改善/悪化トレンド。
- スケール: LoopCount / ElementCount に対する伸び方。
- 失敗/欠測: 測定失敗・片側欠測・比較不能。
- 計測品質: StdDev/Error/外れ値候補。CPU差はここに補助情報として入れる程度。
- 詳細: 選択候補の生測定行。

4. SQLite スキーマと View:
- 通常実行時に DROP VIEW / CREATE VIEW を繰り返さないでください。
- DDL は設計時・スキーマ作成/更新時のみ、通常の読書きは DML/SELECT にしてください。
- スキーマ作成・更新処理と通常読書き処理を明確に分離してください。
- 既存 Work\UI\runs.db の互換維持は不要です。
- 既存DBは捨ててよいです。
- 今後は schema mismatch 時、開発用では既存DBをバックアップリネームして新規作成する自動リセットを入れてください。
- Release/通常運用で無警告に破棄しないでください。
- 既存の BenchmarkPairComparisons は TypeFullName 完全一致で比較して 0 行になる問題があります。
- UI側の比較ロジックに合わせ、通常は method + argumentKey + source tab/pair で比較し、Scalable/重複時だけ必要な discriminator を使ってください。
- 分析に必要な計測値、計算値、fallback 診断値を SQLite に保存し、それを集計する View を定義してください。

必要な View 例:
- BenchmarkFlatResults
- BenchmarkPairComparisons
- BenchmarkTrendResults
- BenchmarkImprovementCandidates
- BenchmarkTimeRatioWorst
- BenchmarkAllocationRatioWorst
- BenchmarkCategorySummary
- BenchmarkHistoryTrend
- BenchmarkScaleTrend
- BenchmarkMissingFailureMatrix
- BenchmarkMeasurementQuality
- BenchmarkFallbackDiagnostics
- BenchmarkSelectedDetail

5. テストと検証:
- Test.Seamless.AdventureWorks2008R2、Test.Seamless.Database 以下のファイルを、Database として使えるテストへ更新してください。
- AdventureWorks2008R2/TPC-H 的な Container/Table/View/Function/TableFunction 呼び出しが、FLinq で展開され、期待結果と一致することをテストしてください。
- Fragments/Pipeline の全分岐ルートをテストしてください。
- Enumerable 全メソッドをテストしてください。
- 生成コード内に System.Linq.Enumerable fallback が残らないことをテストしてください。
- 実行時 fallback カウンタが 0 であることをテストしてください。
- Benchmark UI の選択状態、Run/SourceTab/ページ切替、SQLite保存復元、実行キュー生成、右ペイン分析タブ、改善プロンプト生成をテストしてください。
- 可能なら dotnet build Seamless.slnx と関連 dotnet test を実行してください。
- 全件が重い場合は範囲を分け、未実行・失敗・タイムアウト理由を明記してください。

6. 作業の進め方:
- まず現状を読んで、短い実装計画を出してください。
- ただし計画だけで止まらず、実装・テスト・検証まで進めてください。
- 大規模リファクタリングで壊した API、削除した Fragment/Pipeline 経路、除外した LINQ メソッド、DBスキーマ破棄方針、実行時DDLを避ける設計を必ず説明してください。
- 最終報告では、変更概要、主要ファイル、追加テスト、実行した検証、残リスクを日本語で簡潔にまとめてください。
```

## 補足

上の fenced text を fable5 にそのまま貼る。

この文書自体はプロンプト保存用であり、実装に必要な追加設計が発生した場合は、fable5 側で docs やテストへ反映する。
