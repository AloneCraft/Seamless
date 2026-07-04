# Methods1 ジェネリック化で root が Span に展開されなくなった件

## 症状

`Methods1` ベンチマーク（`this.srcCount(N).AsX<T>().<Operator>(...)` を LINQ 演算子ごとに測る基底クラス）を
**非ジェネリック `Methods1 : Base`（要素型 `int` 固定）→ ジェネリック `Methods1<T> : Base<T> where T:notnull`** に変えた
コミット `985430c2` 以降、多くの演算子ベンチが遅くなった。

体感上の原因は「root（配列 `T[]` / リスト `List<T>`）が `Span` 直走査に展開されなくなった」こと。だが
**その根本は要素型が `T` になったこと自体ではなく、FLinq ソースジェネレータが `Methods1<T>` のメソッドを
傍受対象から丸ごと除外していること**にある。

---

## 根本原因

FLinq は `[LinqIntercept(true)]` の付いた `_Impl` メソッド内の LINQ を、C# インターセプタとして生成した
高速版に差し替える。root が配列/リストなら `ArrayRoot`/`ListRoot` に落ち、`Span` を直接走査するコードになる。

ところが、そもそも「そのメソッドを傍受対象にするか」を決める入口のゲートで弾かれている。
[`LinqTargetDiscovery.cs`](../../Seamless.FLinq/SourceGenerator/LinqTargetDiscovery.cs) の選別条件:

```csharp
// LinqTargetDiscovery.cs
|| method.ContainingType.IsGenericType     // ← Methods1<T> はここで true → return false
// ジェネリック型メンバーは生成メソッド側へ型パラメータを漏らしやすいので除外。
```

- 変換前 `Methods1`（非ジェネリック）: `ContainingType.IsGenericType == false` → 通過 → 各 `_Impl` に高速インターセプタが生成される。
- 変換後 `Methods1<T>`: `IsGenericType == true` → **全 `_Impl` が非傍受** → 生成コードが 1 つも出ない → 素の `System.Linq` が実行される。

### なぜこのゲートが存在するのか

生成された傍受メソッドとヘルパー struct は、元クラスの中ではなく**外側の別の非ネスト静的クラス**へ出力される
（[`LinqInterceptorSourceWriter.cs`](../../Seamless.FLinq/SourceGenerator/LinqInterceptorSourceWriter.cs) の
`インターセプター型ソース構築`）。この静的クラスは元クラスの型パラメータ `T` を宣言していないため、
レシーバ型 `Methods1<T>` や要素型 `T` を名指しできず、そのまま出力するとコンパイル不能になる。
それを避けるための保守的な処置が「ジェネリック含有型は丸ごと除外」である。

### 誤解しやすい点

「要素が `T` になったから Span に展開できなくなった」は**誤り**。フラグメント段
（[`LinqFragmentBuilder.cs`](../../Seamless.FLinq/SourceGenerator/LinqFragmentBuilder.cs) の root 選択）は、
もし傍受まで到達できれば `T[]` も `List<T>` も問題なく Span 化できる:

- `T[]` は `IArrayTypeSymbol` に一致 → `ArrayRoot`
- `List<T>` は `IsListType`（`OriginalDefinition` 比較なので `List<int>` も `List<T>` も一致）→ `ListRoot`
- 要素型名は `"T"` としてそのままレンダリングされる

つまり真因は**フラグメント段に到達する前の discovery 除外**。`T` そのものではない。
（例外として、root の静的型が「配列/リスト」ではなく**裸の型パラメータ `T`**（`ITypeParameterSymbol`）だった場合は
要素型解決に失敗して別経路で Span 不可になるが、Methods1 の root は `T[]`/`List<T>` なのでこれには該当しない。）

---

## 展開コードで見る「非ジェネリック=速い / ジェネリック=遅い」

例として既定の配列ベンチ（`AsX<T>(this T[]) => T[]`、root は `T[]`）の `Aggregate_func` を使う。
元コード（[`Methods1.cs`](Methods1.cs)）:

```csharp
[LinqIntercept(true)]
private void Aggregate_func_Impl() {
    var result = this.srcCount(ImmediateFullScanCount).AsX<T>().Aggregate((acc,x) => Add(acc,x));
    ...
}
```

### 非ジェネリック時（速い）: 生成される高速インターセプタ

`Methods1`（`int` 固定）では `_Impl` が傍受され、root 配列が **struct エニュメレータ＋`Span` 直走査**に落ちる。
`obj/Release` に残る旧世代のスナップショット `*Methods1_cs.g.cs` はまさにこの形（`int[] source`）:

```csharp
private struct OptimizedLinq_Aggregate_func_..._Struct1 : IValueEnumerator<int>, IEnumerator<int> {
    private int[] source;
    private int __e1 = -1;
    public bool TryGetSpan(out ReadOnlySpan<int> span) {
        span = MemoryExtensions.AsSpan(source, __e1 + 1);   // ★配列を Span で直走査
        return true;
    }
    public bool TryGetNext(out int current) {
        if(++__e1 < source.Length) { current = source[__e1]; return true; }
        current = default!; return false;
    }
}

[global::System.Runtime.CompilerServices.InterceptsLocation(1, "...Methods1.cs...")]
public static void OptimizedLinq_Aggregate_func_Impl_...(this global::...Methods1 @this) {
    using var __s0 = new ...Struct1(/* 配列 source */);
    if(__s0.TryGetSpan(out var __span)) {
        // ★境界チェック最小・仮想呼び出しゼロ・ボックス化ゼロの Span ループ
        var acc = __span[0];
        for(int i = 1; i < __span.Length; i++) { acc = acc + __span[i]; }
        ...
    }
}
```

`List<T>` root の場合は `ListRoot` が
[`Root.cs`](../../Seamless.FLinq/SourceGenerator/Linq/Fragments/Root.cs) で
`CollectionsMarshal.AsSpan(source)` を使う同型の Span コードを出す（仮想呼び出し・ボックス化なし、インデックス直走査）。

### ジェネリック時（遅い）: 生成インターセプタが「無い」

`Methods1<T>` では discovery で除外されるため、**`.g.cs` に該当メソッドが 1 つも生成されない**。
呼び出し箇所は `_Impl` の本体をそのまま実行する:

```csharp
// Methods1<T>.Aggregate_func_Impl は傍受されず、素の LINQ がそのまま走る
var result = this.srcCount(N).AsX<T>().Aggregate((acc,x) => Add(acc,x));

// 実際に走るのは:
//   System.Linq.Enumerable.Aggregate<T>(IEnumerable<T> source, Func<T,T,T> func)
//     → source.GetEnumerator() が返す IEnumerator<T> を foreach（インターフェース経由の仮想 MoveNext/Current）
//     → 要素ごとに Func<T,T,T> デリゲート呼び出し
//     → その中で汎用ヘルパー Add(=Value(Number(l)+Number(r))) を経由
//        （値型 T では ToInt/FromInt 変換も要素ごとに発生）
//     → Span 直走査は一切無し
```

`Add` 等は要素型を汎用化するためのラッパ（[`Methods1.cs`](Methods1.cs) の `Number`/`Value`/`Add` …）。
非ジェネリック時は生成コード内で `acc + item`（`int` 直加算）に潰れていたが、ジェネリック＋非傍受では
デリゲート＋ヘルパー呼び出しが要素ごとに残る。

### まとめ（なぜ差がつくか）

| | root の扱い | 列挙 | 演算 |
|---|---|---|---|
| 非ジェネリック（傍受あり） | `T[]`→`ArrayRoot` の `Span` 直走査 | インデックス直参照・仮想呼び出しゼロ | 生成コードにインライン展開 |
| ジェネリック（傍受なし） | `IEnumerable<T>` として素の LINQ | `IEnumerator<T>` の仮想呼び出し | デリゲート＋汎用ヘルパー呼び出し |

---

## 対策（実装済みの生成器拡張）

**目的**: ジェネリック含有型 `Methods1<T>` のメンバーでも傍受を復活させ、`ArrayRoot`/`ListRoot` の `Span` 高速路を取り戻す。

C# インターセプタの制約上、傍受メソッドを収める静的ホルダークラス自体はジェネリックにできない。
そこで**含有型の型パラメータを傍受メソッド側へ移す**方針で、FLinq 生成器を次のとおり拡張した（実装済み・ビルド確認済み）:

1. **discovery ゲートの緩和** — [`LinqTargetDiscovery.cs`](../../Seamless.FLinq/SourceGenerator/LinqTargetDiscovery.cs) の
   `ContainingType.IsGenericType` 一律除外をやめ、「型パラメータと制約を安全に再現できる単純ジェネリック含有型」かつ
   「本体 LINQ にラムダを含まない」場合に限り許可（`再現可能なジェネリック含有型か`）。
2. **含有型の型パラメータ＋制約を捕捉** — `method.ContainingType.TypeParameters`（名前と `notnull`/`class`/`struct`/`new()` 等の制約）を
   捕捉し `型パラメーター制約節生成` で `where` 節を生成。
3. **傍受署名へ反映** — 生成メソッドを `...<T>(this Methods1<T> @this) where T:notnull` の**ジェネリックメソッド**として宣言
   （ホルダークラスは非ジェネリックのまま。[`LinqInterceptorSourceWriter.cs`](../../Seamless.FLinq/SourceGenerator/LinqInterceptorSourceWriter.cs)）。
4. **ヘルパー struct の `<T>` 化** — `OuterTypeParameters` を断片ツリーへ伝播し（`PropagateContainingTypeName`/`PropagateOuterTypeParameters`）、
   Root/Scalar/Set/Concat/Zip/ToArray 各断片の struct 宣言と `EnumeratorTypeName` に外側型パラメータを前置。

この 1〜4 により、配列ベンチ（`Benchmark/DotNet.Linq.Enumerable.FLinq`）では `Methods1<T>` 向けに **38 個のジェネリック傍受メソッド**が
生成され（修正前は 0）、`MemoryExtensions.AsSpan` を用いた root→Span 直走査コードが出力される（＝構造としては高速路が復活する）。

## ただし .NET 10 の UnsafeAccessor 制限で実行時に落ちる（重要）

上記でコンパイルは通るが、**実行時に `System.MissingMethodException` / `MissingFieldException`** が発生する。
原因は **.NET 10 の `[UnsafeAccessor]` が「ジェネリック型の非公開メンバー」を解決できない**こと（method/static/field いずれも不可。
非ジェネリック型なら可）。最小再現で確認済み:

```csharp
public class Base<T> where T:notnull { protected int M(int n) => n; }

[UnsafeAccessor(UnsafeAccessorKind.Method, Name="M")]
static extern int Acc<T>(Base<T> @this, int n) where T:notnull;

Acc<int>(new Base<int>(), 1);   // → MissingMethodException: 'Base`1.M'
```

`Methods1<T>` の各演算子は `srcCount`/`consumer`/`Result`/`MidpointValue` … といった **`Base<T>`/`Methods1<T>` の
protected/private メンバー**を必ず触るため、生成された傍受メソッドは全滅する。

### 現状の安全策（実装済み）

生成器は「ジェネリック型の非公開メンバー用アクセサ（＝ジェネリック UnsafeAccessor）」を出す必要が生じた時点で例外を投げ、
その演算子の傍受を諦めて**元実装（素の `System.Linq`、遅いが正しい）へフォールバック**する（`アクセサ型パラメータ` のガード）。
これにより「コンパイルは通るが実行時 throw」という悪化を避けている。値比較テスト `Methods1_results_match` は合格する。

結果として、**この .NET 制限が解けるかヘルパーを公開するまで、`Methods1<T>` の速度は戻らない**（安全に遅いまま）。

## 速度を実際に戻す選択肢（要判断）

- **(A) ヘルパーメンバーを internal 以上に公開**: 傍受コードは同一アセンブリに生成されるので、`srcCount`/`GetOrCreateByLength`/
  `CreateSourceArray`/`consumer`/`Result`/`MidpointValue`/`src2Count`/`CountForComparison`/`Value` … 演算子が触る**推移的閉包すべて**を
  internal（か public）にすれば UnsafeAccessor を介さず直接呼べ、傍受＋Span が有効化する。ベンチ基盤の可視性を広く変える。
- **(B) `Methods1` を非ジェネリックへ戻す**（コミット `985430c2` の逆）: 元の高速路が即復活。ジェネリック共有は失う。
- **(C) 現状維持**: `Methods1<T>` は遅いが正しいまま。.NET 側の UnsafeAccessor ジェネリック対応を待つ。

### 却下した代替案

- **型パラメータをクラス→メソッドへ移動**: `_Impl` は引数を取らず `T` はクラス状態（`T[]` 配列など）に属するため成立しない。
  さらに引数に型パラメータが残ると別の除外条件（`ContainsTypeParameter`）で再度弾かれる。

### エビデンス

`Benchmark/DotNet.Linq.Enumerable.FLinq/obj/Release` に残る旧 `*Methods1_cs.g.cs` スナップショットはいずれも非ジェネリック版
（`int[] source` の Span 版）で、現行 `Methods1<T>` 形を捉えたものは無い＝genericization 後は傍受コードが出ていなかったことの裏付け。
