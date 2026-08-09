# Benchmark.Dynamic 実装計画

## 1. エグゼクティブサマリー

- Windows専用の独立MAUIアプリ `Benchmark.Dynamic.exe` と、実行単位で起動する `Benchmark.Dynamic.Worker.exe` を新設する。
- `Benchmark.UI`、既存Worker、`UI.Core`、`UI.Storage` へ直接依存しない。既存の表示書式・計測・プロセス停止方式は仕様上の参考に留める。
- 同一の型付きQuery IRから、System.Linq、最適化なしFLinq、最適化済みFLinq、ZLinqを1クラスへ生成する。性能測定はSystem／最適化済みFLinq／ZLinqの3系列、最適化なしFLinqは意味検証専用とする。
- FLinqへ明示Opt-inの大域固定点モードと、構文書き換え・パイプライン正規化の両層を記録するトレースを追加する。既存FLinqの既定動作は変更しない。
- Common Setは全8ソースの積集合、Maximum Setは選択ソースの積集合とする。Capability外の演算子は生成前に完全除外し、System.Linqへの実行時フォールバックは行わない。
- 初期値はQuery数10、N=8、入力要素数1,024、最大深度`min(N,8)`。初回選択はSystem Arrayのみとする。
- 個別Queryの失敗は保存して処理を継続する。意味不一致・呼出先不正・最適化非収束のQueryは性能測定しない。
- 完了条件は、生成→コンパイル→4経路意味検証→3系列測定→DB保存→UI表示が全8ソースで動作し、決定的テストで深い子・左右部分木・連鎖書換え・固定点・意味保存を直接確認できること。

## 2. リポジトリ調査結果

- ソリューションは `Seamless.slnx`、SDKは.NET 10系。既存MAUIアプリは `net10.0-windows`、Roslynは5.3.0、ZLinqは1.5.6、SQLiteはMicrosoft.Data.Sqlite 10.0.9を使用している。
- `DynamicLinq.Runner` と `Shared/DynamicLinq/EnumerableComparisonCatalog.cs` は存在するが、固定メソッド名から単一演算子を選ぶCSVランナーである。型付きランダムツリー、Roslyn実行時コンパイル、ALC解放、DB、UIは持たないため、新アプリの実装基盤にはしない。
- `Test/SourceGenerator/GeneratorTestHelpers.cs` には、`LanguageVersion.Preview`、TPA参照、`FLinqIncrementalGenerator`、`RunGeneratorsAndUpdateCompilation`を使う既存Roslyn試験基盤がある。動的コンパイラはこの構成を実運用向けに発展させる。
- `Seamless.FLinq.Generator.csproj` はRoslyn 5.3.0を参照し、`FLinqIncrementalGenerator` はpublicである。Workerから通常参照してGenerator Driverへ明示登録できる。
- 新規共有`.projitems`は作らない。したがって`.shproj`追加義務は発生しない。新規プロジェクトはすべて通常の`.csproj`として `Seamless.slnx` へ登録する。
- 既存 `Work/UI/Benchmark.db` とそのWAL/SHMは変更しない。

## 3. 現在のBenchmark.UI構造

- `Benchmark/Benchmark.UI/Benchmark.UI.csproj` はWindowsではunpackaged `WinExe` であり、`Benchmark/Shared/UI/UI.projitems` を取り込む。
- `Benchmark/Shared/UI/MainPage.cs` はC#で画面を構築し、Syncfusion `SfDataGrid` とタブを使用する。
- `Shared/UI.Core/BenchmarkSourceTabs.cs`、`BenchmarkSharedModels.cs`、`BenchmarkOrchestrator.cs` は左右2系列・固定ベンチマークカタログへ強く結合しており、3系列の動的Query比較へ安全に流用できない。
- `Benchmark/Worker/Program.cs` のFast計測は破棄2回、測定7サンプル、中央値を主値とし、`GC.GetAllocatedBytesForCurrentThread` を使う。この原則を新Workerの版管理済み固定計測プロファイルへ移植する。
- `Shared/UI.Storage/UiRunStore.cs` は通常Benchmark用version 25、左右列・タブ別表を前提とする。スキーマ不一致時の自動バックアップ／削除再作成は現行リポジトリ規則と合わないため再利用しない。
- 現行UIに復元可能なごみ箱はなく、Run削除は物理削除である。Benchmark.Dynamicでは新規にsoft delete方式を実装する。
- 共有するのはMAUI／Syncfusionのバージョン、見た目、表示単位、ChannelによるUI更新抑制、Worker強制停止という設計原則だけとする。既存コードの移動や一般化は行わず、Benchmark.UIへの回帰を避ける。

## 4. 現在のFLinq最適化構造

- `AlgebraicRewriter.cs` はCount比較→Any、Where＋終端融合、OrderBy.First→MinBy／MaxBy、恒等Select／AsEnumerable除去、Where並べ替えを行う。
- `OptimizeVisitor.cs` はSelect、Join、SelectMany、Union等を対象に述語を押し下げる。
- `ProjectionMergeVisitor.cs` はSelect連鎖、Join系result selector、SelectMany result selector後のSelectを合成する。
- `FLinqIncrementalGenerator.MethodBodyRewriter.cs` は現在、各フェーズを個別に最大8回反復する。全フェーズを包む固定点ではなく、8回目にも変更があった場合は無変更確認なしで終了する。
- 各Visitorは現在ノードを先に試し、成功時は同じpassでreplacementの子を再訪問しない。後続passが再訪問を担う。
- `PipelineBuilder.Normalize.cs` にはDistinct／DistinctBy統合、Take／Skip統合、Reverse相殺、Where融合があるが、現状は1回の規則列のみである。
- 現在、規則名、ノードパス、深さ、before/after、固定点到達、左右部分木、再訪問を外部取得する機構はない。
- 新しい固定点動作は `[LinqIntercept]` の明示Opt-inだけで有効化し、通常ビルドの既定8回動作は維持する。

## 5. PersistedおよびTsavoriteの実際の制約

| 対象 | System.Linq | FLinq root | ZLinq root | 順序・寿命 |
|---|---|---|---|---|
| Array | `T[]` | `T[]` | `FromArray<T>` | ordered、再列挙可 |
| IEnumerable | 順序保証fixture | `IEnumerable<T>` | `FromEnumerable<T>` | ordered、再列挙可 |
| Range | `Enumerable.Range` | Range root | `FromRange` | ordered、再列挙可 |
| List | `List<T>` | 既存Route同様`IEnumerable<T>`へ型消去 | `FromList<T>` | ordered、再列挙可 |
| Persisted(System) | `Set<T>`をBCLで列挙 | `IEnumerable<T>`へ型消去 | `FromEnumerable<T>` | unordered、Dispose必須 |
| Persisted(Seamless) | `Set<T>`を明示的BCL呼出し | 具象`Set<T>` provider root | `FromEnumerable<T>` | unordered、Dispose必須 |
| Tsavorite(System) | `HashSet<T>`をBCLで列挙 | `IEnumerable<T>`へ型消去 | `FromEnumerable<T>` | unordered、同一thread、Dispose必須 |
| Tsavorite(Seamless) | `HashSet<T>`を明示的BCL呼出し | 具象`HashSet<T>` provider root | `FromEnumerable<T>` | unordered、同一thread、Dispose必須 |

- Persistedは `Store.CreateSet<T>`／`CreateTempSet<T>`、Tsavoriteは `Store.Create(...).Create<T>().Create()` が実在する。
- Persisted／Tsavoriteのオブジェクトと列挙子はfixture寿命外へ漏らさない。Tsavoriteは作成・充填・実行・列挙・破棄をWorkerの同一専用threadに閉じる。
- Tsavoriteの`Count`は全走査でありO(1)ではない。Capabilityとは別にCost特性を記録する。
- ZLinqのValueEnumerableは動的メソッド外へ返さず、生成メソッド内でスカラー化または完全materializeする。
- ZLinqとSeamlessの`AsValueEnumerable`競合を避けるため、ZLinq rootは完全修飾static callで生成する。
- Persisted／Tsavoriteを外側rootとするSelectMany、provider rootのOrderBy→ThenBy、直接rootのContains解決はPhase 0の実コンパイル・実行テストで確定する。失敗した組合せは推測で有効化せずCapabilityをfalseにする。
- Seamlessタブも主比較系列はSystem／FLinq／ZLinqとする。`Seamless.Linq.ValueEnumerable` はCapability確認用の既存APIとして検証するが、主グリッドの第4系列にはしない。

## 6. 推奨タブ構造

- 案Aの「実行経路→ソース」を採用する。
- 上位タブ:
  - System
  - Seamless
- System配下:
  - Array
  - IEnumerable
  - Range
  - List
  - Persisted
  - Tsavorite
- Seamless配下:
  - Persisted
  - Tsavorite
- 表示タブと実行対象は分離する。右側の8ソースチェックボックスで1～8件を選び、同じIR群を選択対象へ順次実行する。
- すべてのタブは同じ `動的ソース結果画面モデル` を使用し、`SourceTarget` をコンストラクタ引数として渡す。結果コレクションはRun単位の共有ストアをSourceTargetでfilterする。
- 実行中もタブ切替と既存行の詳細閲覧を許可する。

## 7. 他のタブ案を採用しない理由

- ソースファミリー→経路:
  - Array／IEnumerable／Range／ListにはSeamless子タブがなく階層が不均一になる。
  - 実際にFLinqが具象provider rootか型消去rootかを理解しにくい。
- 8タブのフラット構成:
  - 現行Benchmark.UIとは整合するが、Persisted／TsavoriteのSystem／Seamless差が名称だけに埋もれる。
  - 将来SourceTargetが増えた際に横幅と検索性が悪化する。
- 経路→ソースは、Capability、DB検索条件、実際のFLinq root、フォールバック禁止を最も明確に対応付けられる。

## 8. Seamless未対応演算子の処理方針

- 初期版は完全除外を採用する。
- Capability判定はメソッド名の存在だけでなく、入力／出力型、順序契約、オーバーロード解決、FLinq interceptor生成、ZLinq root型、結果比較可能性まで含める。
- Capability外の演算子はランダム候補へ入れない。Roslynコンパイル失敗を能力探索に利用しない。
- 具象Set／HashSetのインスタンス`Contains`が選ばれる場合は、FLinq演算子として扱わない。明示的な安全テンプレートが実際にinterceptされる場合だけ有効化する。
- FLinq内部でprovider-native融合からFLinq自身のbuffer／一般fragmentへ移ることはSystemフォールバックではない。CapabilityへNativeFusion範囲を記録する。
- 予期しないSystem.Linq bind、generator未生成、ZLinq以外へのbindは `InvocationTargetMismatch` として保存し、そのQueryを測定しない。

## 9. Common SetとMaximum Set

- Common Set:
  - 全8 SourceTarget×System／FLinq／ZLinqで意味的に実行可能な演算子の積集合。
  - UIで選択中のソースに依存せず、同じProfileVersionでは定義を固定する。
- Maximum Set:
  - 実行時にチェックされたSourceTargetのCapability積集合。
  - 1ソース選択時はそのソースの最大集合となる。
- 実行対象が複数でも同一QueryIndexへ同一IRを使用する。
- 「生成行数」はQuery IR件数である。Query数10、ソース3件なら、同じ10 IRを各ソースへ適用して30 SourceExecutionを作る。
- Profile IDは、OperatorSetMode、選択SourceTargetの昇順列、CapabilityProfileVersion、OperatorSetVersionのcanonical JSONからSHA-256で生成する。
- ソース未選択、Nと最大深度の不整合、選択Profileで指定Nを完成できない場合は実行前validation errorとし、Runを開始しない。

## 10. Benchmark.Dynamicのプロジェクト構成

| プロジェクト | Target | 責務 |
|---|---|---|
| `Benchmark/Benchmark.Dynamic` | `net10.0-windows` MAUI WinExe | View、ViewModel、DI、設定、履歴、ごみ箱 |
| `Benchmark/Benchmark.Dynamic.Core` | `net10.0` | Query IR、Capability、乱数生成、source emitter、比較モデル、Worker protocol |
| `Benchmark/Benchmark.Dynamic.Infrastructure` | `net10.0` | SQLite、Worker process client、設定／履歴repository |
| `Benchmark/Benchmark.Dynamic.Worker` | `net10.0` Exe | Roslyn、FLinq driver、ALC、fixture、検証、較正測定 |
| `Benchmark.Dynamic.Tests` | `net10.0-windows` | Core／Worker／DB／ViewModelのunit・integration |
| `Benchmark.Dynamic.UITests` | `net10.0-windows` | Appium smoke |

依存方向:

- App → Core、Infrastructure
- Infrastructure → Core
- Worker → Core、Seamless、Seamless.FLinq.Generator、ZLinq、Roslyn
- Tests → App、Core、Infrastructure、Worker
- WorkerはDBへ直接書き込まない。
- Appのビルド後targetでWorkerをビルドし、exe／dll／deps／runtimeconfig／pdbを出力先へコピーする。
- `AssemblyName=Benchmark.Dynamic`、`ApplicationId=com.seamless.benchmark.dynamic`、`WindowsPackageType=None` とする。
- 各プロジェクトは1namespaceに統一する。UIコードの型・メンバー名は既存規則どおり日本語とする。
- `Shared/DynamicLinq`、`Benchmark.UI`、`UI.Core`、`UI.Storage`、既存Workerは参照しない。

## 11. Query IR

主要public型:

- `SourceTarget`
- `SourceKind`
- `ExecutionPath`
- `QueryIrDocument`
- `QueryNode`
- `SourceNode`
- `UnaryQueryNode`
- `BinaryQueryNode`
- `NestedQueryNode`
- `TerminalQueryNode`
- `QueryValueType`
- `QueryCardinality`
- `QueryOrderSemantics`
- `DynamicQueryValue`

閉じた型集合:

- `Int`
- `Long`
- `Bool`
- `Double`
- `IntPair`
- `IntGroup`
- 上記のSequence／Scalar

契約:

- SourceNodeはNへ数えない。
- Unary、Binary、Nested、Terminalは各1として数える。
- 左右部分木・内側サブクエリもNへ合算する。
- rootは必ずTerminalであり、Nの最小値は1。
- ラムダ、key selector、scalar comparisonテンプレートはLINQ演算子数へ含めない。
- 各nodeは決定的Node ID、Input／Output type、cardinality上下限、order semantics、empty safety、関連Operator IDを持つ。
- Binary nodeは左右、Nested nodeはouter／innerを明示し、単なる文字列式として保持しない。
- IR JSONには`IrSchemaVersion`を持たせ、保存済みIRは将来版で再実行できない場合も読取・表示可能にする。

## 12. CapabilityProfile

`QueryCapabilityProfile` は以下を持つ。

- Profile ID／Version
- SourceTarget
- engine別root route
- 中間／終端Operator ID
- Input／Output type遷移
- `Ordered`／`Unordered`／`TotalOrdered`
- 再列挙可否
- Dispose／same-thread要否
- provider-native root可否
- materialize-before-return要否
- empty sequence制約
- cardinality上限式
- 使用可能なlambda template
- Binaryの左／右適格性
- Nested outer／inner適格性
- 期待するSemanticModel symbol
- FLinq生成期待
- System fallback禁止
- Cost metadata

`OperatorDescriptor` は以下を一元管理する。

- 安定したOperator IDとVersion
- arity／category／phase
- 型遷移
- 順序遷移
- cardinality推定
- 安全precondition
- System／FLinq／ZLinq emitter
- SourceTarget別Capability
- 関連FLinq rule ID
- 結果比較方式

Phase 0のroute matrix testが通った組合せだけをprofileへ登録する。未確認APIを仮にtrueとしない。

## 13. ランダム生成

- 基準Seedは符号なし64bit値とし、10進／16進入力を受け付ける。
- Query seed、fixture seed、node seedは、基準Seed・QueryIndex・用途名をcanonical UTF-8化し、SHA-256から64bitを取得して分離する。
- PRNGアルゴリズムと選択順は`GeneratorVersion`へ含める。候補Operatorは安定ID順に並べてから乱数選択する。
- exact-N生成は残りnode budgetとdepth budgetを使う再帰文法で行い、完成可能な候補だけを選ぶ。失敗後の無制限retryは行わない。
- 入力payloadは重複しない決定的整数集合とし、Array／IEnumerable／Range／List／Persisted／Tsavoriteで同じ値集合を使用する。
- Query×Sourceごとにfresh fixtureを生成し、4検証経路で共有して行終了時に破棄する。
- 安全上限:
  - 入力要素: 100,000
  - 最終／中間materialize: 100,000
  - N: 64
  - 最大深度: 32
  - Join出力推定: 100,000
  - SelectMany展開推定: 100,000
- Phase 1～8の中心演算子:
  - Where、Select、Distinct
  - SelectMany
  - Join
  - Concat、Union
  - OrderBy、ThenBy
  - 安全なTake
  - Count、LongCount、Any、All、Sum、Min、Max、ToArray、ToList
- CapabilityによりCommonでは一部が除外されても、Maximumで安全なSourceTargetに対して使用できる。
- Phase 9は直後の優先タスクとして、GroupJoin、Except、Intersect、By系、位置依存、全materializer、Aggregate等を網羅追加する。
- 副作用、現在時刻、共有Random、0除算、overflow依存、危険なempty terminal、無限列挙はtemplateとして定義しない。
- shrinkingは手動操作とし、別Workerで時間・候補数予算内に決定的順序で実行する。元IRと縮小IRは両方保存する。

## 14. Roslynコンパイル

1 SourceExecutionにつき1つの生成クラスと1つのCompilationを作る。

生成メソッド:

- `RunSystem`
- `RunFLinqUnoptimized`
- `RunFLinq`
- `RunZLinq`

属性:

```csharp
[LinqIntercept(true,Optimizations=LinqOptimizations.None)]
RunFLinqUnoptimized(...)

[LinqIntercept(
    true,
    Optimizations=LinqOptimizations.All,
    ConvergenceMode=LinqConvergenceMode.GlobalFixedPoint,
    EmitOptimizationTrace=true)]
RunFLinq(...)
```

コンパイル条件:

- `LanguageVersion.Preview`
- `NullableContextOptions.Enable`
- `OptimizationLevel.Release`
- deterministic=true
- unsafe=false
- 一意なassembly／type名はRun ID、QueryIndex、SourceTarget、source hashから生成
- SourceTextはUTF-8
- PEとportable PDBをMemoryStreamへEmit

参照解決:

- Worker起動時にTPAを読み込み、Core、Seamless、FLinq、ZLinq、Persisted、Tsavoriteの実ロードAssembly.Locationを追加する。
- 参照一覧はWorker内でcacheするが、Compilation、SyntaxTree、generated assembly、delegateはcacheしない。
- 固定ローカルパスを埋め込まない。

検証:

- Systemは`System.Linq.Enumerable`へのbindを要求する。
- ZLinqは完全修飾したZLinq symbolと期待`From*` rootを要求する。
- FLinq入力呼出しは期待するBCL extensionへbindし、Generator Run Resultに対応interceptorが生成されたことを要求する。
- インスタンスメソッド解決、想定外extension、FLinq未生成は失敗とする。
- Roslyn runtime compilationではSeamless analyzerを自動能力探索へ使わない。Phase 0のtemplate試験でAnalyzer結果を検証し、runtimeは確定Capabilityを使用する。

保存時間内訳:

- SourceText／SyntaxTree
- CSharpCompilation作成
- Generator Driver
- Emit
- Assembly load
- Type／Method解決
- Delegate bind

UIは合計compile timeを表示し、詳細とDBには全内訳を保存する。

## 15. AssemblyLoadContext

- 単位はQuery×Sourceごとに1 collectible ALC。
- 4メソッドは同じ動的Assembly／ALCに置く。
- ALCの`Load`は、Core、Seamless、ZLinq等をDefault Contextの既存Assemblyへ統一する。動的Assemblyだけをcollectible contextへロードする。
- Delegateは `Func<object?,object?,DynamicQueryValue>` へ統一し、primary／secondary fixtureをobjectで受けて生成メソッド内でcastする。
- 動的Assembly固有型、ValueEnumerable、匿名型、MethodInfo、TypeをALC外へ返さない。
- 実行結果はCore側の閉じた`DynamicQueryValue`へmaterializeして返す。
- 実行後はdelegate、MethodInfo、Type、Assembly、stream参照を解放して`Unload`する。
- `WeakReference`で解放を確認し、GC確認は全計測区間外で行う。
- 3回のGC確認後も生存する場合は `AssemblyUnloadFailed` を保存する。連続3件でWorkerを終了し、UI側が新Workerで残りを継続する。
- 多数Query試験でprocess private bytesと未解放ALC数を監視する。

## 16. 実行と結果比較

処理順:

1. fresh fixture作成
2. 生成・コンパイル・呼出先検証
3. 4経路を各1回、完全実行
4. 全結果比較
5. 一致時だけ3系列をwarmup／較正／測定
6. 結果送信
7. fixture破棄、ALC unload確認

比較契約:

- System／最適化なしFLinq／最適化済みFLinq／ZLinqの4結果を比較する。
- 最適化なしFLinqは性能測定しない。
- ordered／total-ordered結果は要素数・全要素・位置を完全比較する。
- unordered結果は全要素をcanonical valueへ変換し、重複数を含むmultisetで比較する。
- scalar、IntPair、IntGroupは型ごとの再帰比較を行う。
- checksumだけで成功判定しない。
- 任意の実行例外は、4経路で同じであっても安全生成契約違反として失敗扱いにする。
- 不一致時は各結果全文、最初の差分位置／値、例外、生成source、IR、traceを安全上限内で保存する。
- 不一致・呼出先不正・非収束は測定しない。
- 個別Query失敗後も次Queryへ進む。Worker通信破損、DB不能、Worker process喪失だけをRun致命障害とする。
- 実行順はQueryIndex、SourceOrdinal、SampleIndexによる3系列Latin rotationとし、順序列もDBへ保存する。

## 17. 三つの時間計測

### Query生成時間

含む:

- IR生成
- IR validation
- SourceTarget別の4メソッドsource生成
- source文字列組立

分離保存:

- `IrGenerationNanoseconds`
- `SourceEmissionNanoseconds`
- UI表示は両者の合計

含まない:

- fixture生成
- SyntaxTree以降
- DB／UI
- 実行

### Roslynコンパイル時間

含む:

- SourceText
- SyntaxTree
- Compilation
- Generator Driver
- Emit
- ALC load
- Type／Method解決
- Delegate bind

MetadataReferenceの初回cache生成はWorker startup eventとして別記録し、Query compile timeへ含めない。

### 実行時間

版管理済み `Balanced-v1`:

- validation実行後、全系列共通で1回だけfull GCを測定外で実行
- 各系列warmup 2回
- 1 invocationから倍増し、25ms以上になるまで較正
- 最大反復数1,048,576
- 測定7サンプル
- 外れ値除去なし
- 主値はns/op中央値
- 平均、最小、最大、標準偏差、標準誤差も保存
- allocationは各サンプルで `GC.GetAllocatedBytesForCurrentThread` の差分をop数で正規化
- Gen0／1／2 collection deltaもサンプル保存
- 3系列をsampleごとに決定的ローテーション
- 同一専用threadで較正・warmup・測定
- JIT、比較、DB、UI、ALC unload、GC unload確認は主実行時間へ含めない

raw sampleをすべて保存し、集計値から復元しない。

## 18. 再帰最適化の直接検証

FLinqへ生成属性を追加する。

```csharp
internal enum LinqConvergenceMode
{
    Legacy,
    GlobalFixedPoint
}
```

`LinqInterceptAttribute`へ以下を追加する。

- `ConvergenceMode`
- `EmitOptimizationTrace`

構文層の1 round:

1. AlgebraicRewriter
2. 変更時rebind
3. OptimizeVisitor
4. 変更時rebind
5. ProjectionMergeVisitor
6. 変更時rebind

- round全体が無変更ならFixedPoint。
- triviaを除いたcanonical構文fingerprintの再出現でCycleDetected。
- 最大128 roundで変更が続けばIterationLimit。
- rebind失敗はRebindFailed。
- Query syntax loweringは固定点round前に1回だけ行う。

Pipeline層は、既存のDistinct→DistinctBy→Take→Skip→Reverse→Where規則列を1 roundとし、同じ停止・cycle・128 round上限で別固定点化する。

Trace JSON:

- SchemaVersion
- Method key
- layer／phase／rule ID
- round／pass／visit番号
- Node ID／NodePath
- depthまたはstage index
- parent kind
- left／right／inner
- before／after
- changed
- revisited
- layer termination

Summary:

- RewriteCount
- MaxRewriteDepth
- OptimizationPassCount
- ReachedFixedPoint
- AppliedRuleNames
- RevisitedAfterRewrite
- LeftSubtreeOptimized
- RightSubtreeOptimized

traceはopt-in時だけ、Base64 JSONコメントを持つ別generated sourceとして出力し、`GeneratorDriver.GetRunResult()`から回収する。static global sinkは使わない。

非収束時はtraceとsourceを保存するが、Emit／性能測定へ進めない。

決定的直接検証:

- 深さ2以上の子で書換え
- 二項演算子の左右双方で訪問・書換え
- Count比較→Any後のWhere融合など異なる規則の連鎖
- 10段以上のSelectが旧8回境界を超えて固定点へ到達
- 最適化済み入力のrewrite 0
- `Distinct().Reverse().Reverse().Distinct()` がPipelineで複数roundを必要とする
- fake transformerによるcycle／上限検出
- 4経路の意味保存

## 19. ランダムテストの位置付け

- 決定的テストが、再帰訪問、左右処理、連鎖、固定点、意味保存を直接示す。
- ランダムテストは未知の組合せ、反例、Capability漏れ、訪問漏れを探索する補助であり、形式的証明とは表現しない。
- PR CI:
  - 固定Seed corpus 256件
  - N=1～16
  - Array Common／Maximum
  - Persisted／Tsavoriteの小規模代表seed
- 手動／nightly:
  - 10,000件以上
  - N最大64
  - 全SourceTarget
- 失敗署名はStatus、Roslyn diagnostic ID、例外型、最初の差分、非収束理由をcanonical化する。
- 手動shrinkerは同じ失敗署名を維持する候補だけを採用する。
- shrinking順序はterminal簡略化、Unary除去、左／右簡略化、predicate簡略化、selector恒等化、入力要素数削減とする。
- 性能値にはCI閾値を設けず、正しさ・再現性・メモリ解放だけを合否条件にする。

## 20. UIと右端設定領域

- 既存Benchmark.UIに合わせ、MainPageはC#で構築し、Syncfusion DataGrid／TabViewを使用する。
- 右側設定領域はGridSplitterで幅変更可能とし、既定320px、最小280px、最大480px、折り畳み可能。幅と折り畳み状態を保存する。
- 設定:
  - 8 SourceTargetチェック
  - Query数: 1～1,000、既定10、step 1
  - N: 1～64、既定8、step 1
  - 入力要素数: 1～100,000、既定1,024
  - Seed入力／新規Seed生成
  - Common／Maximum
  - 詳細設定の最大深度: 1～`min(N,32)`、既定`min(N,8)`
  - 実行
  - キャンセル
- 初回はSystem Arrayだけを選択する。2回目以降は前回選択を復元する。
- 実行時に設定スナップショットを固定し、設定・ソース選択・履歴削除／復元を無効化する。
- 実行中も表示タブ切替、既存行選択、詳細閲覧は許可する。
- 進捗:
  - 現在のQuery／SourceTarget
  - 完了数
  - コンパイル失敗
  - 意味不一致
  - 非収束
  - 実行例外
  - キャンセル状態
- `Preferences`にはUIの前回値だけを保存し、各Runの確定条件はDBへ完全snapshotする。

## 21. グリッドと詳細パネル

主グリッド列:

- 選択
- Status
- QueryIndex／短縮式
- N／Depth
- 生成時間
- コンパイル時間
- System中央値／allocation
- FLinq中央値／allocation／System比
- ZLinq中央値／allocation／System比
- 意味一致
- RewriteCount
- FixedPoint

平均、最小、GC、raw samples、診断全文は主列へ追加しない。

下部スプリット詳細パネル:

- Summary
- Source
- IR
- Results
- Diagnostics
- Optimization Trace
- Measurements

動作:

- 初期高さは画面の35%、折り畳み可、Splitter位置を保存する。
- Sourceには生成クラス全文とコピー操作を用意する。
- Traceはlayer→round→eventのtree表示と、before/after差分を持つ。
- Measurementsはraw samples、実行順、較正値、集計値を表示する。
- Run全体再実行、Query単体再実行、設定だけ復元してSeed再生成を別コマンドにする。
- 正確な再実行は保存IRを使用し、元Run／Queryを上書きしない。

## 22. DBスキーマ

DB:

- 通常配置: `Work/UI/Benchmark.Dynamic.db`
- solution rootが見つからない場合のみLocalApplicationData配下
- `PRAGMA foreign_keys=ON`
- WAL
- busy timeout
- schema version 1

正規化テーブル:

1. `DynamicSchemaInfo`
   - Version、AppliedAtUtc
2. `DynamicRuns`
   - RunId、ParentRunId、Status、開始／終了、app／runtime／OS／CPU／Git情報
   - GeneratorVersion、CapabilityProfileVersion、OperatorSetVersion
   - Query数、N、MaximumDepth、InputCount、BaseSeed、Mode
   - MeasurementProfile IDと実値JSON
   - Canceled、FailureSummary、DeletedAtUtc
3. `DynamicRunSources`
   - RunId、SourceKind、ExecutionPath、Ordinal
4. `DynamicQueries`
   - QueryId、RunId、ParentQueryId、QueryIndex、QuerySeed
   - IR JSON、IR hash、N、Depth、ResultType、OrderSemantics
   - IR生成時間、DerivationKind
5. `DynamicSourceExecutions`
   - ExecutionId、QueryId、SourceTarget、CapabilityProfile ID／JSON
   - Status、生成source全文／hash
   - SourceEmission、SyntaxTree、Compilation、Generator、Emit、Load、Bind時間
   - Rewrite summary、固定点終了理由、ALC解放結果
6. `DynamicValidationResults`
   - ExecutionId、ImplementationKind
   - result summary、失敗時full result、例外、symbol validation
7. `DynamicMeasurements`
   - ExecutionId、ImplementationKind
   - loop数、sample数、warmup、中央値、平均、最小、最大、標準偏差、標準誤差
   - allocation/op、GC集計
8. `DynamicMeasurementSamples`
   - MeasurementId、SampleIndex、ExecutionOrder
   - elapsed、operation count、allocation、Gen0／1／2
9. `DynamicDiagnostics`
   - ExecutionId、Stage、ImplementationKind、Severity、DiagnosticId、Message、Location、Properties
10. `DynamicOptimizationEvents`
    - ExecutionId、Sequence、Layer、Phase、Rule、Round、Visit、NodePath、Depth／StageIndex、Before、After、Flags
11. `DynamicRunEvents`
    - RunId、Sequence、Timestamp、EventKind、Message、worker情報

制約:

- Queryは`UNIQUE(RunId,QueryIndex)`。
- SourceExecutionは`UNIQUE(QueryId,SourceKind,ExecutionPath,Attempt)`。
- 通常削除は `DynamicRuns.DeletedAtUtc` 更新だけ。
- ごみ箱復元はNULL化、完全削除時だけ子表を`ON DELETE CASCADE`する。
- 自動期限削除は行わない。
- Run開始、SourceExecution完了、キャンセル確定をそれぞれtransaction境界とする。
- Workerは逐次eventを送信し、UI側のsingle writer queueだけがDBを書き込む。
- schema fileは `Storage/Schema/001_initial.sql` とする。DBが存在しない場合だけ初期作成する。
- version不一致時は自動削除／自動migrationせず書込みを拒否する。既存DBを変更する際はDB・WAL・SHMの明示バックアップ後、専用PowerShell migrationを直接適用する。

## 23. 非同期実行とキャンセル

- UIの `動的実行調停サービス` がRun rowを作り、専用Worker processを1つ起動する。
- IPCはversion付きJSON Linesとし、stdinをcontrol、stdoutをevent、stderrをdiagnostic log専用にする。
- WorkerではIPC readerと結果writerをbackground化し、fixture／Roslyn／測定は1本の専用threadで直列実行する。
- source生成・コンパイルを次Queryと並列化しない。測定中のCPU／GC干渉を避ける。
- UI更新は100ms、進捗集計は250ms以下に抑制する。DBはSourceExecution完了ごとにcommitする。
- Cancel:
  1. UI設定を維持したまま新規投入停止
  2. WorkerへCancellationToken通知
  3. Query／コンパイル／warmup／sample境界で協調停止
  4. 3秒で終了しなければprocess treeをkill
- 完了・失敗済み行は保持する。
- 処理中行はCanceled、未開始Query／SourceはNotStarted、RunはCanceledとして保存する。
- Run再開機能は初期版に含めない。再実行は新しい子Runとして作る。

## 24. 実装Phase

### Phase 0：既存構造確認

- 目的: 未確認APIを実テストで確定する。
- 対象: `Test`、既存FLinq／Persisted／Tsavorite。
- 新規: provider route、SelectMany、ThenBy、Contains、ZLinq bindのcharacterization tests。
- 変更: 既存実装なし。
- 新規型: route matrix test fixture。
- 完了: 8 SourceTarget×各engineの期待symbol／runtime結果一覧が確定。
- テスト: 対象Roslyn shape tests、provider runtime tests。
- リスク: 未対応API発見。
- Gate: 失敗組合せをCapability=falseとして記録できること。

### Phase 1：アプリケーション骨格

- 目的: 独立exe、DI、タブ、設定、ダミーグリッドを起動する。
- 対象: App、Core、Infrastructure、Worker、Tests、UITests、solution。
- 新規: 全csproj、MainPage、画面モデル、Worker ping protocol。
- 変更: `Seamless.slnx`。
- 新規型: `動的ベンチマーク画面モデル`、`SourceTarget`、protocol DTO。
- 完了: `Benchmark.Dynamic.exe` が起動し、経路→ソースタブと設定UIを表示。
- テスト: Release build、ViewModel unit、Appium起動smoke。
- リスク: MAUI／Syncfusion初期化。
- Gate: Benchmark.UI非参照で起動できること。

### Phase 2：最小Query IR

- 目的: Array、int、Where／Select、単一Terminalの決定生成。
- 対象: Core、Tests。
- 新規: IR、descriptor、PRNG、source emitter。
- 変更: ダミー結果供給をCore生成へ接続。
- 新規型: QueryNode群、OperatorDescriptor、QueryIrDocument。
- 完了: 同条件でIR JSONとsourceがbyte一致し、exact Nを満たす。
- テスト: Seed再現、N、depth、型、cardinality。
- リスク: exact-N生成不能。
- Gate: retryに依存せず全許可Nを生成できること。

### Phase 3：Roslyn

- 目的: Systemコードをcompile、load、delegate実行、unloadする。
- 対象: Worker、Core、Tests。
- 新規: DynamicQueryCompiler、MetadataReferenceProvider、DynamicAssemblyLoadContext。
- 変更: Worker protocol。
- 新規型: CompilationResult、LoadedQuery。
- 完了: System Arrayを100回compile／unloadしてALC残留ゼロ。
- テスト: diagnostics、Release options、参照解決、WeakReference。
- リスク: default／collectible context型分裂。
- Gate: Core型を安全に跨いで結果を返せること。

### Phase 4：三実装比較

- 目的: 4検証経路と3主系列を生成し、呼出先と結果を確認する。
- 対象: Worker、Core、FLinq参照、Tests。
- 新規: engine別emitter、symbol validator、result comparer。
- 変更: 生成クラスを4メソッド化。
- 新規型: ImplementationKind、ValidationResult。
- 完了: System／no-opt FLinq／FLinq／ZLinqが同一結果で、期待経路を通る。
- テスト: instance method誤解決、ZLinq完全修飾、FLinq interceptor確認。
- リスク: using／overload競合。
- Gate: compile成功だけではなく経路検証が成功すること。

### Phase 5：計測とDB

- 目的: 3時間区間、Balanced-v1、SQLite、UI表示を接続する。
- 対象: Worker、Infrastructure、App、Tests。
- 新規: MeasurementEngine、RunStore、schema、WorkerClient。
- 変更: gridを実結果へ接続。
- 新規型: MeasurementSummary、RawSample、RunRepository。
- 完了: 最小Queryが生成→測定→DB→再読込まで通る。
- テスト: 区間境界、sample集計、allocation、transaction、cancel途中保存。
- リスク: 計測へのIPC／DB混入。
- Gate: 測定threadでDB／UI処理が走らないこと。

### Phase 6：再帰性の決定的検証

- 目的: FLinqの明示Opt-in大域固定点と両層traceを実装する。
- 対象: Seamless.FLinq.Generator、Test、Worker。
- 新規: FixedPointRunner、OptimizationTrace。
- 変更: 3 Visitor、MethodBodyRewriter、Pipeline Normalize、生成属性。
- 新規型: convergence enum、termination、trace event。
- 完了: 深い子、左右、連鎖、固定点、Pipeline複数roundをtraceで直接assert。
- テスト: deterministic shape／meaning／cycle／limit／legacy regression。
- リスク: 既存生成結果への波及。
- Gate: Opt-inなしの既存generated sourceが不変であること。

### Phase 7：ランダムなツリー

- 目的: closed types、Binary、Nested、安全な深さ／cardinalityを追加。
- 対象: Core、Worker、Tests。
- 新規: binary／nested descriptors、safety analyzer、failure signature。
- 変更: generator、source emitter、comparer。
- 新規型: IntPair、IntGroup、CardinalityBudget。
- 完了: ArrayでN最大64の再帰Queryを決定生成・比較できる。
- テスト: property tests、左右／inner budget、上限超過拒否。
- リスク: size explosion。
- Gate: runtime guardに到達する前に危険IRを拒否できること。

### Phase 8：全ソース対応

- 目的: 8 SourceTarget、Capability、複数選択、Common／Maximumを完成する。
- 対象: Core、Worker、App、Tests。
- 新規: SourceFixtureFactory、Capability registry、source checkbox UI。
- 変更: tabs、DB filtering、replay。
- 新規型: SourceFixture、QueryCapabilityProfile。
- 完了: 各SourceTargetで同一IRの3系列測定と4経路検証が成功。
- テスト: same-thread、Dispose、unordered比較、route matrix、fresh fixture。
- リスク: provider固有制約。
- Gate: compile失敗を能力判定へ使っていないこと。

### Phase 9：演算子拡張

- 目的: 初期中心演算子の直後に、実在APIを網羅追加しshrinker／履歴操作を完成する。
- 対象: 全新規プロジェクト、Test、CI。
- 新規: 追加descriptor、shrinker、replay、trash、Appium smoke。
- 変更: Capability version、OperatorSet version、DB／UI detail。
- 新規型: ShrinkRequest、ShrinkResult、Replay metadata。
- 完了: 候補演算子を「対応／Capability限定／未対応」に分類し、対応分を全route test。
- テスト: operator matrix、random corpus、replay、trash、UI E2E。
- リスク: 組合せ爆発、DB肥大。
- Gate: 追加Operatorごとにsymbol／意味／size／order testが揃うこと。

## 25. 変更予定ファイル一覧

| ファイルパス | 区分 | 役割 | 主な型 | Phase | 既存影響 |
|---|---|---|---|---|---|
| `Seamless.slnx` | 変更 | 新規プロジェクト登録 | ― | 1 | Solution Explorerのみ |
| `.github/workflows/dotnet-ci.yml` | 変更 | Dynamic build/test | ― | 9 | CI時間増加 |
| `Benchmark/Benchmark.Dynamic/Benchmark.Dynamic.csproj` | 新規 | Windows MAUI exe | ― | 1 | なし |
| `Benchmark/Benchmark.Dynamic/MauiProgram.cs` | 新規 | DI／起動 | `MauiProgram` | 1 | なし |
| `Benchmark/Benchmark.Dynamic/MainPage.cs` | 新規 | タブ／grid／右設定／詳細 | `MainPage` | 1,5,8 | なし |
| `Benchmark/Benchmark.Dynamic/ViewModels/*.cs` | 新規 | UI状態／履歴／ごみ箱 | 日本語画面モデル群 | 1,5,9 | なし |
| `Benchmark/Benchmark.Dynamic.Core/Benchmark.Dynamic.Core.csproj` | 新規 | Core library | ― | 1 | なし |
| `Benchmark/Benchmark.Dynamic.Core/QueryIr.cs` | 新規 | 型付きIR | QueryNode群 | 2 | なし |
| `Benchmark/Benchmark.Dynamic.Core/CapabilityProfile.cs` | 新規 | 能力表 | QueryCapabilityProfile | 8 | なし |
| `Benchmark/Benchmark.Dynamic.Core/RandomQueryGenerator.cs` | 新規 | exact-N生成 | RandomQueryGenerator | 2,7 | なし |
| `Benchmark/Benchmark.Dynamic.Core/QuerySourceEmitter.cs` | 新規 | 4メソッドsource生成 | QuerySourceEmitter | 2,4 | なし |
| `Benchmark/Benchmark.Dynamic.Core/ResultComparison.cs` | 新規 | ordered／multiset比較 | DynamicQueryValue | 4,7 | なし |
| `Benchmark/Benchmark.Dynamic.Core/WorkerProtocol.cs` | 新規 | IPC DTO | WorkerRequest／Event | 1,5 | なし |
| `Benchmark/Benchmark.Dynamic.Infrastructure/Benchmark.Dynamic.Infrastructure.csproj` | 新規 | DB／process client | ― | 1 | なし |
| `Benchmark/Benchmark.Dynamic.Infrastructure/DynamicWorkerClient.cs` | 新規 | Worker lifecycle | DynamicWorkerClient | 5 | なし |
| `Benchmark/Benchmark.Dynamic.Infrastructure/DynamicRunStore.cs` | 新規 | SQLite repository | DynamicRunStore | 5,9 | なし |
| `Benchmark/Benchmark.Dynamic.Infrastructure/Storage/Schema/001_initial.sql` | 新規 | DB version 1 | ― | 5 | 新DBのみ |
| `Benchmark/Benchmark.Dynamic.Infrastructure/Storage/Apply-BenchmarkDynamicMigration.ps1` | 新規 | backup付き明示migration | ― | 5 | Dynamic DBのみ |
| `Benchmark/Benchmark.Dynamic.Worker/Benchmark.Dynamic.Worker.csproj` | 新規 | Worker exe | ― | 1 | なし |
| `Benchmark/Benchmark.Dynamic.Worker/Program.cs` | 新規 | protocol host | WorkerHost | 1,5 | なし |
| `Benchmark/Benchmark.Dynamic.Worker/DynamicQueryCompiler.cs` | 新規 | Roslyn／Generator Driver | DynamicQueryCompiler | 3,4,6 | なし |
| `Benchmark/Benchmark.Dynamic.Worker/DynamicAssemblyLoadContext.cs` | 新規 | collectible load | DynamicAssemblyLoadContext | 3 | なし |
| `Benchmark/Benchmark.Dynamic.Worker/SourceFixtureFactory.cs` | 新規 | 8 source fixtures | SourceFixtureFactory | 8 | なし |
| `Benchmark/Benchmark.Dynamic.Worker/MeasurementEngine.cs` | 新規 | Balanced-v1 | MeasurementEngine | 5 | なし |
| `Seamless.FLinq.Generator/SourceGenerator/Interceptor.cs` | 変更 | 生成属性へOpt-in追加 | LinqConvergenceMode | 6 | 既定不変 |
| `Seamless.FLinq.Generator/SourceGenerator/LinqGeneratorModels.cs` | 変更 | convergence伝搬 | generator models | 6 | 既定不変 |
| `Seamless.FLinq.Generator/SourceGenerator/FLinqIncrementalGenerator.MethodBodyRewriter.cs` | 変更 | 大域固定点 | FixedPointRunner利用 | 6 | Opt-inのみ |
| `Seamless.FLinq.Generator/SourceGenerator/AlgebraicRewriter.cs` | 変更 | rule trace | AlgebraicRewriter | 6 | Opt-in trace |
| `Seamless.FLinq.Generator/SourceGenerator/OptimizeVisitor.cs` | 変更 | rule trace | OptimizeVisitor | 6 | Opt-in trace |
| `Seamless.FLinq.Generator/SourceGenerator/ProjectionMergeVisitor.cs` | 変更 | rule trace | ProjectionMergeVisitor | 6 | Opt-in trace |
| `Seamless.FLinq.Generator/SourceGenerator/Linq/Fragments/PipelineBuilder.Normalize.cs` | 変更 | Pipeline固定点／trace | PipelineBuilder | 6 | Opt-inのみ |
| `Seamless.FLinq.Generator/SourceGenerator/FixedPointRunner.cs` | 新規 | cycle／limit／fixed point | FixedPointRunner | 6 | Opt-inのみ |
| `Seamless.FLinq.Generator/SourceGenerator/OptimizationTrace.cs` | 新規 | trace schema／collector | Trace types | 6 | 通常無効 |
| `Seamless.FLinq.Generator/Properties/AssemblyInfo.cs` | 新規 | Testへのinternal公開 | InternalsVisibleTo | 6 | Test限定 |
| `Test/SourceGenerator/OptimizationFixedPointTests.cs` | 新規 | 決定的FLinq試験 | test fixtures | 0,6 | なし |
| `Test/SourceGenerator/DynamicRouteShapeTests.cs` | 新規 | provider route確定 | test fixtures | 0,8 | なし |
| `Benchmark.Dynamic.Tests/Benchmark.Dynamic.Tests.csproj` | 新規 | unit／integration | ― | 1 | なし |
| `Benchmark.Dynamic.Tests/**/*.cs` | 新規 | Core／Worker／DB／VM試験 | 各test class | 2～9 | なし |
| `Benchmark.Dynamic.UITests/Benchmark.Dynamic.UITests.csproj` | 新規 | Appium | ― | 1 | なし |
| `Benchmark.Dynamic.UITests/**/*.cs` | 新規 | UI smoke | session／tests | 1,9 | なし |

## 26. テスト計画

- FLinq:
  - legacy mode generated source不変
  - Opt-in固定点、trace決定性
  - 深い子、左右、連鎖、idempotence
  - Pipeline複数round
  - cycle、limit、rebind failure
  - 意味保存
- IR:
  - 同じ条件→同じIR／source
  - exact N／depth
  - 型遷移
  - terminal位置
  - cardinality上限
  - Capability外Operatorなし
- Roslyn:
  - diagnostics保存
  - TPA／project参照
  - System／FLinq／ZLinq symbol
  - interceptor実在
  - Release／nullable／preview
- ALC:
  - 1,000 Query load／unload
  - WeakReference解放
  - dynamic型流出なし
- Source:
  - 8 route
  - Persisted／Tsavorite Dispose
  - Tsavorite same-thread
  - SelectMany／ThenBy／Contains spike
- 比較:
  - ordered位置差
  - unordered重複差
  - scalar／pair／group
  - full-result mismatch
- 計測:
  - warmup除外
  - 較正上限
  - 7 samples
  - median／allocation／GC
  - deterministic rotation
- DB:
  - schema version
  - transaction
  - partial cancel
  - replay親子関係
  - soft delete／restore／purge
  - version不一致時の書込み拒否
- UI:
  - タブ対応
  - source checkbox
  - 数値範囲
  - 実行時disable
  - cancel
  - details
  - history／trash
- CI主要コマンド:
  - `dotnet test .\Test\Test.csproj -c Debug -f net10.0-windows --filter "FullyQualifiedName~OptimizationFixedPointTests|FullyQualifiedName~DynamicRouteShapeTests"`
  - `dotnet test .\Benchmark.Dynamic.Tests\Benchmark.Dynamic.Tests.csproj -c Debug -f net10.0-windows`
  - `dotnet build .\Benchmark\Benchmark.Dynamic\Benchmark.Dynamic.csproj -c Release -f net10.0-windows`
  - `dotnet build .\Benchmark.Dynamic.UITests\Benchmark.Dynamic.UITests.csproj -c Debug`
- Appium実行は専用環境／手動job、通常CIではUITestsのbuildまでとする。

## 27. リスクと対策

| リスク | 対策 |
|---|---|
| LINQ間のAPI形状差 | OperatorDescriptorへengine別emitterとsymbol testを持つ |
| Seamless未対応 | Capabilityで生成前除外、fallback禁止 |
| instance method誤選択 | SemanticModelとinterceptor結果を必須gate化 |
| Roslyn参照解決 | TPA＋実ロードAssembly.Location、固定パス禁止 |
| ALC leak | Query×Source単位、default型統一、WeakReference、Worker再起動 |
| 固定点のcycle | fingerprint履歴、128 round上限、非収束測定禁止 |
| Opt-in変更の既存回帰 | Legacy既定維持、generated source regression |
| JIT混入 | validation／warmupを主測定外へ分離 |
| 実行順偏り | sample単位の決定的Latin rotation |
| 計測overhead | 較正、ns/op正規化、raw samples保存 |
| 要素数爆発 | static cardinality解析＋runtime materialize guard |
| IR型不整合 | closed type algebraと生成前validation |
| unordered順序差 | IR order semantics、multiset完全比較 |
| Query再現不能 | IR／source全文、全version、Seed、選択sourceを保存 |
| Generator変更 | 保存IR replayとSeed再生成を別操作にする |
| 数百行のUI負荷 | Channel、100ms batching、virtualized grid |
| DB肥大 | normalized samples、Run単位purge、index、soft delete可視化 |
| Worker hang | cooperative cancel後3秒でprocess tree kill |
| Persisted一時file残留 | `finally` Dispose＋run startup orphan cleanup |
| 既存Benchmark.UI回帰 | 直接参照・共有化・既存DB変更を行わない |

## 28. 実装開始時の最小エンドツーエンド単位

最初の受入可能な垂直スライスは次とする。

- Windowsの `Benchmark.Dynamic.exe` が起動する。
- System→Arrayタブだけを表示対象とする。
- Query数1、N=3、入力32、固定Seed。
- IRは `Where → Select → Count` または `ToArray`。
- 1生成クラスへ4メソッドを出力する。
- Roslyn compile、collectible ALC load、4経路意味比較を行う。
- 一致時に3系列をBalanced-v1で測定する。
- 生成／compile／executionを別計測する。
- Query／SourceExecution／measurement／diagnosticを新DBへ保存する。
- グリッドと下部Source／IR／Measurements詳細へ表示する。
- RunキャンセルとALC解放を確認する。
- FLinq Opt-in traceで少なくとも1件の書換えとFixedPointを表示する。

この単位が通るまで、全8ソースや演算子網羅へ進まない。

## 29. Plan承認後に最初に実行する具体的な作業

1. 既存の関連FLinq／providerテストを変更前baselineとして実行する。
2. `Test/SourceGenerator/DynamicRouteShapeTests.cs` を先に追加し、Persisted／TsavoriteのSelectMany、ThenBy、Contains、ZLinq完全修飾、FLinq interceptorの実際の可否を固定する。
3. テスト結果から最初のCapability matrixを確定し、未対応組合せをfalseにする。
4. その後に6つの新規プロジェクトをscaffoldし、`Seamless.slnx`へ登録する。
5. Phase 1の独立起動・タブ・ダミーグリッドが通った時点で、Phase 2の最小IRへ進む。

既定条件はWindowsのみ、System Array初期選択、Query 10、N=8、入力1,024、Common Set、fallbackなし、Balanced-v1、設定固定、個別失敗継続、途中結果保存で確定する。
