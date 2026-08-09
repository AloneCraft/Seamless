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
- `dotnet pack Seamless.FLinq/Seamless.FLinq.csproj -c Release`: analyzer TFM、依存、重複ランタイムDLLを監査。