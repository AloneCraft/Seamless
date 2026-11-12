---
canonical_target: Document/Code/Internal/FLinqRegistryInternalDesign.md
confidence: high
review_status: draft
---

# FLinq Registry Internal Design

## 責務

`OperatorRegistry`はFLinq Source Generator内部の演算子メタデータ、型付きFactory、ProviderNative方針、Root方針、Pipeline能力を集中管理する。外部拡張APIではない。

## 構成

| 要素 | 責務 |
|---|---|
| `OperatorKey` | メソッド名とVariant名による一意識別 |
| `OperatorDescriptor<TFragment,TArguments>` | Semantic情報からの型付きBindとFragmentの直接生成 |
| `IOperatorDescriptor` | 登録一覧と整合性検証の非Generic境界 |
| `RootOperatorDescriptor<TFragment>` | `Range`、`Repeat`、`Empty`、`InfiniteSequence`、`Sequence` の型付き生成と、ソースを透過する `AsEnumerable` の登録 |
| `ProviderNativeTerminalProfile` | Provider terminal、述語、materialize、`Contains` lookupの宣言 |
| `OperatorCoverageKind` | Fragment/Root/Excludedを含む公開API Coverage契約 |

`OperatorRegistry` は Category、Capability、Priority、適用 Predicate を登録値として保持する。`PipelineStageCapabilityTable` は Registry から能力を取得する。ProviderNative 終端の選択はプロファイルで行う。

## 不変条件

1. `OperatorKey`は一意である。
2. 同一メソッドのPriorityは衝突しない。
3. FactoryのFragment型と引数型はそれぞれ`Fragment`、`FragmentArguments`から派生する。
4. ProviderNative capabilityを持つVariantは対応するProviderNativeプロファイルを持つ。
5. 全公開`System.Linq.Enumerable` API名はRegistryでCoverageされる。対象外は`Excluded`で明示する。
6. Root categoryはRoot coverageだけを持つ。

不変条件違反は`FLINQ006`、単体テスト、およびGenerator実行時検証で検出する。

## データフロー

Target DiscoveryがRegistryを参照してCoverageと対象可否を判断する。Fragment Builderは候補をPriority順に取り出し、各VariantのPredicateと型付きBindを適用する。ProviderNativeとRootはそれぞれProfile/Root Descriptorを通じて生成する。Pipeline能力はRegistryから導出する。

## 互換性

Generatorは`netstandard2.0`で動作する。生成されたinterceptorは`Seamless.Linq net10.0`を使用する。Registry追加時に`Seamless.Linq`の公開APIやFLinqの公開Generator APIを変更しない。
## 生成サービス

Registryは演算子選択の正本のまま維持し、その前後の生成責務を次のinternalサービスに分離する。

| サービス | 入力 | 出力/責務 |
|---|---|---|
| `LinqConfigurationResolver` | method symbol, compilation | 属性継承済み不変設定 |
| `LinqTargetDiscoveryService` | invocation syntax | `Linq傍受対象型` |
| `LinqExpressionAnalyzer` | target method | lowering対象と外側呼出し解析 |
| `LinqOptimizationExecutor` | Pipeline stages, configuration, trace | 決定的なstage正規化 |
| `LinqFragmentBuilder` | `GenerationPipeline` | Registry Bind済みFragmentと補助コード |
| `GeneratedSourceRenderer` | 入力ファイル内targets | interceptor source、trace文書 |

`GenerationState` と `FragmentBuildContext` は対象メソッド単位で生成される。状態は対象間で共有せず、Registry descriptorの型付きBindへ明示的に渡す。これにより並列生成時も匿名record、採番、trace、識別子、UnsafeAccessor候補が漏洩しない。

`LinqConfigurationResolver` は発見時に継承を含む属性を解決し、`LinqGenerationConfiguration` に固定する。`LinqOptimizationExecutor` は `PipelineBuilder` に段の正規化を委譲し、設定と trace を扱う。`LinqFragmentBuilder` は `GenerationPipeline` を通じて Root、ProviderNative、ラムダを組み立てる。

`SequenceEqual` は比較子の有無で Variant を分ける。比較子なしの Fragment は比較子をフィールドやコンストラクター引数に保持せず、Span の形や要素型に応じて Span 比較または既定の等価比較を使う。

## Renderer境界と明示Fragment Visitor

Fragmentの子要素はGetChildFragments()とGetChildFunctions()による明示Visitorで収集します。PipelineTerminalとProviderNative終端のPipelineEnumerable、各Fragment Variantのsource・second・lambdaを対象に含め、Fragmentインスタンスのフィールド反射には依存しません。FragmentRenderContextは含有型名、外側型パラメータ、Counter instrumentationをimmutableな値として保持します。

Coordinatorは入力ファイルごとのtarget集合をGeneratedSourceRenderModelへ固定し、GeneratedSourceRendererへ渡します。Rendererは既存のFragment文字列生成を呼び出すだけで、生成ソースの分割、Hint名、診断位置、trace順序を変更しません。

## Render契約の責務境界

RegistryはFragmentの選択・Bind・Factoryの正本として維持し、FragmentからRendererへ渡す情報をRenderPlanへ集約する。

`EnumeratorExpressionRenderModel`、`DirectConsumeSourceRenderModel`、`ScalarExecutionRenderModel` は、列挙式の構築、直接消費時の Span 準備、スカラー終端の列挙をそれぞれ扱う。

| Render契約 | 所有する不変条件 |
|---|---|
| `ClosureRenderPlan` | capture/hoisted/delegateの順序、重複除去、constructor/factory引数一致 |
| `EnumeratorRenderPlan` | struct、field、Current、constructor、TryGetNext、probe、Disposeの描画順 |
| `DirectConsumeRenderPlan` | direct span、TryGetSpan、TryGetNext fallback、Dispose順 |
| `GeneratedCodeBlock` | Leaf、入れ子ブロック、インデント、改行、順序 |
| `MethodBodyRenderPlan` | scalar using/TryGetNext、consumer、Consume、materializeの順序 |

既存のFragmentプロパティとraw文字列は最下層Leafへの互換アダプターとして段階的に残す。Fragmentは意味論と最適化結果を決め、RenderPlanは順序と構造を保持し、最終文字列描画を担当する。これによりclosure・enumerator・生成文字列の密結合を、評価順とDispose契約を変えずに分離できる。

`MethodBodyRewriter` は通常文、`TryGetNonEnumeratedCount`、`UnsafeAccessor` の収集を扱う。生成ソースの Hint 名と分割、診断位置、trace 順序を維持する。対象メソッドごとの `GenerationState`、`FragmentBuildContext`、RenderPlan は共有せず、並列生成時の採番、匿名構造体、`UnsafeAccessor`、trace を分離する。Registry の型付き Factory 契約を維持し、Fragment の走査と Bind は明示的に行う。
