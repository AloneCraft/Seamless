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