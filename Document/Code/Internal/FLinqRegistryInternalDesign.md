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
| `RootOperatorDescriptor<TFragment>` | 特殊Rootの型付き生成 |
| `ProviderNativeTerminalProfile` | Provider terminal、述語、materialize、lookupの宣言 |
| `OperatorCoverageKind` | Fragment/Root/Excludedを含む公開API Coverage契約 |

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
## 第1段階: Registryの上位に置く生成サービス

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

第2段階の対象はFragment内部のclosure/enumerator/文字列生成であり、Registry契約・既存Fragmentの意味論を変えずに描画契約またはIRへ移す。

## Renderer境界と明示Fragment Visitor

Fragmentの子要素はGetChildFragments()とGetChildFunctions()による明示Visitorで収集します。PipelineTerminalとProviderNative終端のPipelineEnumerable、各Fragment Variantのsource・second・lambdaを対象に含め、Fragmentインスタンスのフィールド反射には依存しません。FragmentRenderContextは含有型名、外側型パラメータ、Counter instrumentationをimmutableな値として保持します。

Coordinatorは入力ファイルごとのtarget集合をGeneratedSourceRenderModelへ固定し、GeneratedSourceRendererへ渡します。Rendererは既存のFragment文字列生成を呼び出すだけで、生成ソースの分割、Hint名、診断位置、trace順序を変更しません。

これは第1段階の境界です。closure、enumerator、生成文字列の密結合は第2段階でIRまたは描画契約へ分離します。