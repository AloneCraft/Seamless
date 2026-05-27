# Fragments 設計

## クラス階層

```
Fragment (abstract)  ........  全LINQ断片の基底。コード生成の部品を提供
├── Enumerable (abstract)  ..  IEnumerable<T> を返す操作
│   └── BufferedSequence (abstract)  結果をListにバッファ→列挙
├── Scalar (abstract)  ......  単一値を返す操作
│   └── BufferedScalar (abstract)   結果を即時計算→1回yield

FuncFragment (abstract)  ...  ラムダ/デリゲートの表現
├── LambdaFunc  .............  staticラムダ（インライン展開可能）
└── ParameterFunc  ..........  インスタンスデリゲート（実行時呼び出し）
```

---

## Fragment のメンバー

| メンバー                              | 役割                                       |
|---------------------------------------------|--------------------------------------------|
| `StructMethod`                              | ラムダを包む補助structの宣言コード          |
| `GetEnumeratorStruct`                       | `IValueEnumerator<T>` struct の定義全体     |
| `GetTryGetNextBody`                         | `TryGetNext(out T)` の本体コード            |
| `GetCurrentExpression`                      | `Current` プロパティの式                    |
| `EnumeratorTypeName`                        | struct の型名（`Struct{番号}`）             |
| `GetEnumeratorExpression`                   | `new Struct{番号}(...)` の式                |
| `GetEnumeratorPrimaryConstructorParameters` | primary constructor の引数リスト       |
| `GetEnumeratorFields`                       | struct のフィールド定義                     |
| `GetEnumeratorConstructor`                  | struct のコンストラクタ（必要な場合）        |

---

## Enumerable の追加メンバー

| メンバー                       | 役割                                           |
|--------------------------------|------------------------------------------------|
| `ElementType`                  | このシーケンスの要素型                          |
| `GetEnumerableExpression`      | `ValueEnumerable<Struct, T>` の構築式           |
| `GetEnumeratorDisposeBody`     | Dispose 処理（既定は `e{番号}.Dispose()`）      |

---

## Scalar の追加メンバー

| メンバー                              | 役割                               |
|---------------------------------------|------------------------------------|
| `DefineScalarEnumeratorStruct(T)`     | スカラー結果用の struct を生成      |

---

## BufferedSequence の追加メンバー

| メンバー            | 役割                                          |
|---------------------|-----------------------------------------------|
| `BuildExpression`   | `"Build(source, ...)"` 静的メソッド呼び出し    |
| `BuildMembers`      | `Build()` メソッド定義（中で List を作る）     |
| `StructSources`     | 子 enumerator の struct 定義                   |

---

## BufferedScalar の追加メンバー

| メンバー              | 役割                                          |
|-----------------------|-----------------------------------------------|
| `ComputeExpression`   | `"Compute(source, ...)"` 静的メソッド呼び出し  |
| `ComputeMembers`      | `Compute()` メソッド定義（中で値を計算）       |
| `StructSources`       | 子 enumerator の struct 定義                   |

---

## FuncFragment のメンバー

| メンバー              | 役割                                              |
|-----------------------|---------------------------------------------------|
| `DefineLambda`        | ラムダ本体 `=> expr;`                             |
| `ReturnTypeName`      | 戻り値型                                          |
| `ParameterTypeNames`  | 引数型一覧                                        |
| `ParameterNames`      | 引数名一覧                                        |
| `CanInlineAssign`     | staticラムダならtrue→コードに直接埋め込み          |
| `CanGenerateMethod`   | helper struct を生成できるか                      |
| `InlineBody`          | インライン展開する式本体                           |
| `DefineMethod(name)`  | `static TResult name(args)` を生成                |
| `RenderInlineAssign(dest, num, args)` | 代入文として展開                   |
| `RenderInlineExpression(args)`       | 式として展開                       |

---

## 変数名ルール（`番号` で一意化）

| プロパティ      | 生成例        | 用途                              |
|-----------------|---------------|-----------------------------------|
| `e`             | `e0`          | Enumerator                        |
| `item`          | `ite0`        | 現在要素                          |
| `currentValue`  | `currentValue0` | Current 用フィールド             |
| `yielded`       | `yielded0`    | 1回だけ出力フラグ                  |
| `result`        | `result0`     | 集約結果                          |
| `Struct`        | `Struct0`     | struct 型名                       |
| `key`           | `key0`        | GroupBy/Join のキー               |
| `state`         | `state0`      | 列挙状態                          |
| `a`             | `a0`          | インデックス                      |
| `group`         | `group0`      | グループ変数                      |
| `lookup`        | `lookup0`     | Lookup/Dictionary                 |
| `outerValue`    | `outerValue0` | Join の外側要素                   |
| `Head`          | `Head0`       | ループ先頭ラベル                  |
| `Start`         | `Start0`      | 再判定ラベル                      |
| `Skip`          | `Skip0`       | スキップラベル                    |
