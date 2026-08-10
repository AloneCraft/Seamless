# FLinq Source Generator

## Registry中心設計

`Seamless.FLinq` は `System.Linq.Enumerable` 呼び出しを解析して、入力ファイル単位の interceptor ソースを出力する Roslyn incremental generator です。演算子探索・適用・能力判定は `SourceGenerator/Linq/Registry/` の `OperatorRegistry` を唯一の正本とします。

- `OperatorKey` はメソッド名とVariant名で演算子を一意に識別します。
- `OperatorDescriptor<TFragment,TArguments>` は型付き `Bind` と直接Factoryを保持します。共通の `IOperatorDescriptor` は一覧・検証用の境界です。
- `OperatorCategory`、`OperatorCoverageKind`、`OperatorCapability`、明示Priority、型付き適用Predicateを登録値として保持します。
- `ProviderNativeTerminalProfile` はProvider Nativeの終端種別、述語の扱い、materialize terminal、Contains lookupを宣言します。生成経路にメソッド名switchは置きません。
- `RootOperatorDescriptor<TFragment>` はRange/Repeat/Empty/InfiniteSequence/Sequenceを型付きFactoryで生成します。`AsEnumerable`はソースを透過する特殊Rootとして同じRegistryに登録します。
- `PipelineStageCapabilityTable` は独自の表を持たず、Registryの能力判定へ委譲します。

## Coverageと診断

公開 `System.Linq.Enumerable` API名はRegistryのCoverage一覧と照合します。実装対象外は `OperatorCoverageKind.Excluded` として明示し、暗黙のallowlistは設けません。重複Key、Priority競合、Factory型、Coverage、ProviderNativeプロファイル整合性はGenerator初期化時に検証し、不整合時は `FLINQ006` を報告します。

## 配布互換性

Generatorアセンブリは `netstandard2.0`、Roslyn依存は `Microsoft.CodeAnalysis.CSharp` / `Microsoft.CodeAnalysis.Analyzers` 5.6.0です。生成コードのランタイムは `Seamless.Linq` の `net10.0` を前提とし、FLinq NuGetには同一パッケージ版の依存だけを記載します。`Seamless.Linq.dll`はFLinqパッケージへ同梱しません。

## 検証

- `Test/SourceGenerator/LinqFragmentCoverageTests.cs`: 公開API Coverage、Key、Priority、Factory型、Capability契約。
- `Test/SourceGenerator/IncrementalCacheTests.cs`: 入力ファイル単位のIncremental Cache分離。
- `Test.System.Linq.Enumerable.FLinq`: ReleaseのBenchmark-as-test。
- `dotnet pack Seamless.FLinq.Generator/Seamless.FLinq.Generator.csproj -c Release`: analyzer TFM、依存、重複ランタイムDLLを監査。
## 第1段階: Fragmentを維持した責務分離

第1段階では既存の `Fragment`、`PipelineBuilder`、各演算子Variant、closure/enumerator の生成アルゴリズムを保持する。Generator内は次の内部サービスを通る。

1. `LinqConfigurationResolver` が属性・継承設定を対象発見時に解決し、`LinqGenerationConfiguration` として対象モデルに固定する。
2. `LinqTargetDiscoveryService` と `LinqExpressionAnalyzer` が対象とLINQ式の解析結果を生成する。
3. 対象メソッドごとの `GenerationState` が識別子、Fragment、UnsafeAccessor、匿名record registry、最適化traceを所有する。ThreadStatic/ambient stateは使用しない。
4. `LinqOptimizationExecutor` が `PipelineBuilder` のstage正規化を設定・trace付きで実行する。
5. `LinqFragmentBuilder` が既存の `GenerationPipeline` を介して型付きRegistry Bind、Root、ProviderNative、lambda処理を組み立てる。
6. `GeneratedSourceRenderer` と出力Coordinatorが入力ファイル単位でソース、診断、traceを集約して `AddSource` する。

`SequenceEqual` は比較子なしVariantを専用Fragmentとして出力する。このVariantは `IEqualityComparer<T>` のfield・constructor引数を持たず、組み込み型は2引数Span比較、その他は `EqualityComparer<T>.Default` を用いる。明示比較子Variantは従来どおり保持する。

第2段階では、Fragment内部に残るclosure、enumerator、文字列組立を独立IRまたは描画契約へ分離する。第1段階ではこの生成形状を意図的に維持する。

### Renderer境界

GeneratedSourceRenderModelはCoordinatorからGeneratedSourceRendererへ渡す入力ファイル単位のスナップショットです。Renderer内部の既存文字列生成は第1段階では変更せず、diagnostic集約、trace収集、AddSourceの順序を維持します。FragmentごとのFragmentRenderContextは対象メソッド単位で置換更新され、並列生成間で共有されません。