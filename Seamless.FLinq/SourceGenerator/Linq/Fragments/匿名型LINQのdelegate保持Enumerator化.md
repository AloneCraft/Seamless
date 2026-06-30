# 匿名型LINQのdelegate保持Enumerator化 実装計画

## 背景

現在、匿名型を含むLINQ式に対しては interceptor.cs 内に以下の特殊パスがある：
- `匿名型LINQ実行文追加52` → `匿名型Select生成53` / `匿名型GroupBy生成54`
- Select/GroupBy は foreach + List/Dictionary 即時評価（ループ展開）
- その他のLINQメソッドは System.Linq.Enumerable に委譲（部分最適化）

しかし匿名型を返すラムダでも、**デリゲートを Enumerator のフィールドに保持すれば struct Enumerator による遅延評価が可能**である。この方針で Fragment 側に統合する。

## 統一方針：FlatLoop を削除し Enumerator に一本化

現在のコード生成パスは2つ存在する：

| パス | 使用箇所 | 問題 |
|------|---------|------|
| **Enumerator** | `フラグメント実行文追加` → `TryGetNext` 状態機械 | 全Fragmentの基本パス |
| **FlatLoop** | `GetFlatLoopCode()` → foreach/for 直接ループ展開 | 一部Fragmentのみ（`SupportsFlatLoop=true`）|

これらは同一ロジック（Whereならフィルタ、Selectなら射影）を異なる方法で実装しており、**DRY違反**かつメンテナンスコスト増大の原因。

**対応**: `SupportsFlatLoop` と `GetFlatLoopCode()` を削除し、Enumerator に統一する。

## コアアイデア：汎用的なdelegate保持モード

特定のメソッドだけを special-case するのではなく、
**すべての Fragment で FuncFragment が匿名型を返す場合に自動的に delegate保持モードへ切り替わる**ようにする。

```csharp
// Fragment.cs の FuncAssign/FuncInvoke 内
if (func.CanInvokeAsDelegate) {
    // デリゲート保持: Enumerator フィールドに Func<TSource, TResult> として保持
    // 匿名型は TResult の型推論で解決される
    return func.DelegateInvokeExpr;  // "this._selector.Invoke(item)"
}
if (func.CanInlineAssign) {
    return func.RenderInlineExpression(arguments);  // 従来のインライン展開
}
throw LambdaInlineUnsupported(func, methodName);
```

これにより Fragment 側のコードは一切変更不要になる（Fragment.cs の基底メソッドだけで対応）。

## 変更内容

### 1. FuncFragment（Func.cs）にプロパティ追加

```csharp
// 追加プロパティ（全Fragmentに影響ゼロのデフォルト値）
public virtual bool CanInvokeAsDelegate => false;
public virtual string? DelegateTypeName => null;     // "global::System.Func<TSource, TResult>"
public virtual string? DelegateFieldName => null;    // Enumeratorのフィールド名（一意名）
public virtual string? DelegateInvokeExpr => null;   // "this._field.Invoke(item)"
public virtual string? DelegateConstructorArg => null; // Enumeratorコンストラクタに渡す式
```

### 2. AnonymousDelegateFuncFragment（新規ファイル）

```csharp
internal sealed class AnonymousDelegateFuncFragment : LambdaFunc {
    public string DelegateTypeName { get; init; }
    public string DelegateFieldName { get; init; }
    public string DelegateInvokeExpr { get; init; }
    public string DelegateConstructorArg { get; init; }

    public override bool CanInlineAssign => false;  // インライン化しない
    public override bool CanInvokeAsDelegate => true;
}
```

### 3. 関数式フラグメント生成() に匿名型検出分岐追加（interceptor.cs）

ラムダが匿名型を返す場合、AnonymousDelegateFuncFragment を生成する。
判定は「ラムダ本体が new { ... } を含む」または「delegateType に匿名型が含まれる」。

```csharp
// 関数式フラグメント生成() 内での分岐
if (ラムダが匿名型を生成する) {
    // デリゲート保持モードの FuncFragment を生成
    // ラムダ本体をインライン展開せず、Func<TSource, TResult> として保持
    fragment = new AnonymousDelegateFuncFragment {
        ReturnTypeName = ...,        // 匿名型だが型推論で解決される
        ParameterTypeNames = ...,
        DelegateTypeName = $"global::System.Func<{argTypes}, {retType}>",
        DelegateFieldName = $"_anonDel{uniqueNumber}",
        DelegateInvokeExpr = $"this.{field}.Invoke({args})",
        DelegateConstructorArg = ラムダ式.ToString(),
    };
    return true;
}
```

これにより、**FuncFragment を引数に取るすべての Fragment**（Select, Where, Join, GroupJoin, OrderBy, DistinctBy, GroupBy, AggregateBy, CountBy, ExceptBy, IntersectBy, UnionBy, SelectMany 等）が自動的に delegate保持モードに対応する。

### 4. Fragment.cs の FuncAssign/FuncInvoke/FuncAssignCurrent 変更

現在のメソッド群は `CanInlineAssign` のみをチェックしている。
`CanInvokeAsDelegate` を先にチェックするように変更する。

```csharp
private protected string FuncInvoke(FuncFragment func, string methodName, params string[] arguments) {
    if (func.CanInvokeAsDelegate) {
        return func.DelegateInvokeExpr;
    }
    if (func.CanInlineAssign) {
        return func.RenderInlineExpression(arguments);
    }
    throw LambdaInlineUnsupported(func, methodName);
}
```

同様に `FuncAssign`, `FuncAssignCurrent`, `FuncInvokeCondition`, `FuncAssignFlat` も修正。
※ `FuncAssignFlat` は FlatLoop パス専用だが、Enumerator 統一後も削除されるまでは残存するため同様に対応。

### 5. Fragment.cs の Enumerator フィールド/コンストラクタ生成変更

現在の Enumerator struct 生成では閉包変数（CapturedLocals）のみをフィールドに持つ。
`CanInvokeAsDelegate == true` の FuncFragment がある場合、
**デリゲートフィールドとコンストラクタ引数を動的に追加する**。

変更対象メソッド：
- `GetEnumeratorFields` → 閉包変数フィールド + デリゲートフィールド
- `GetEnumeratorPrimaryConstructorParameters` → 閉包変数引数 + デリゲート引数
- `GetEnumeratorConstructor` → 閉包変数代入 + デリゲート代入（primary constructor でない場合）
- `ClosureArgsSuffix` → 閉包変数引数 + デリゲート引数（コンストラクタ呼び出し側）
- `ClosurePrimaryCtorSuffix` → 閉包変数引数 + デリゲート引数（primary constructor 宣言側）
- `ClosureFields` → 閉包変数 + デリゲートフィールド
- `ClosureFieldAssignments` → 閉包変数代入 + デリゲート代入

これらは現在の ClosureXxx メソッドと同様のパターンで、FuncFragment の配列から `CanInvokeAsDelegate == true` なものをフィルタして追加する。

### 6. Fragment.cs の PrimaryConstructor 対応

```csharp
// コンストラクタ引数
$"TEnum {e}{ClosurePrimaryCtorSuffix(funcs)}, {func.DelegateTypeName} {func.DelegateFieldName}"

// フィールド代入
$"this.{func.DelegateFieldName} = {func.DelegateFieldName};"
```

### 7. FlatLoop の削除

削除するメンバー：

| 削除対象 | ファイル | 理由 |
|---------|---------|------|
| `SupportsFlatLoop` プロパティ | Fragment.cs | Enumerator 統一により不要 |
| `GetFlatLoopCode()` 仮想メソッド | Fragment.cs | 同上 |
| `FuncAssignFlat()` メソッド | Fragment.cs | FlatLoop専用。`FuncAssign` で代用可能 |
| `FuncInvokeFlat()` メソッド | Fragment.cs | 同上 |
| `GetFlatLoopCode()` の実装 | Join.cs, GroupJoin.cs（各SupportsFlatLoop=trueなFragment） | 同上 |
| フラグメント実行文追加 の SupportsFlatLoop 分岐 | interceptor.cs | FlatLoop 削除により常に Enumerator パス |

これによりコード生成パスが単一になる。

### 8. interceptor.cs からの削除

削除するメソッド（すべて Fragment 側で自動対応されるため）：

| 削除するメソッド | 理由 |
|---------|------|
| `匿名型Select生成53` 全体 | Fragment 側で delegate保持 + Select Fragment が対応 |
| `匿名型GroupBy生成54` 全体 | Fragment 側で delegate保持 + GroupBy Fragment（BufferedSequence）が対応 |
| `匿名型LINQ実行文追加52` 全体 | 上記削除により不要に。匿名型検出は Fragment 生成時に自動的に対応 |
| `フラグメント実行文追加` の FlatLoop分岐 | SupportsFlatLoop 削除により常に Enumerator パス |

維持するメソッド：

| 維持するメソッド | 理由 |
|---------|------|
| `匿名型警告Descriptor`（SEAMLESS001） | 診断ポリシーはinterceptor側。匿名型検出時に警告を出すのは引き続き価値がある |

### 9. FlatLoopの残置について

`SupportsFlatLoop`, `GetFlatLoopCode()`, `FuncAssignFlat()`, `FuncInvokeFlat()` は
デバッグ・テスト・およびAIへのルーチンヒントのために**当面は残す**が、
長期的には上記の通り削除する。

## 部分インライン化（ラムダ内LINQの再帰的Fragment化）

### 課題

匿名型を返すラムダの内部にさらにLINQ呼び出しが含まれる場合、delegate保持だけでは内側のLINQが `System.Linq.Enumerable` に委譲されたままになる。

```csharp
S.Select(p => new { p, e = S.Select(q => q + q) });
//                       ↑ 内側のLINQ → delegate.Invoke で実行時評価
```

### 目標

内側のLINQもコンパイル時に Fragment 展開し、生成コード上で struct Enumerator に置き換える。

```csharp
// 期待する生成コード（イメージ）
private static TResult Helper<TSource, TResult>(TSource p, TSrc innerSource) {
    var __inner = new ValueEnumerable<SelectEnumerator, int>(
        new SelectEnumerator(innerSource.GetEnumerator(), q => q + q));
    return new { p = p, e = __inner };
}
```

### 既存の類似機構：InlinedScalarFunc

現在はスカラーLINQ（Any, Count, First 等）に対して `InlinedScalarFunc` が存在する。
ラムダ内のLINQがスカラーを返す場合、再帰的にFragment化し、static helper メソッドとして生成している。

### 追加する機構：InlinedSequenceFunc（仮）

スカラー版をシーケンス版にも拡張する。

```csharp
// InlinedSequenceFunc（シーケンスLINQ部分インライン化）
internal sealed class InlinedSequenceFunc : LambdaFunc {
    public bool CanInlineAssign => false;
    public bool CanInvokeAsDelegate => false;  // delegateも使わない
    public bool RequiresStatementInlining => true;  // 複数文が必要

    public override string RenderInlineAssign(string destination, int number, string[] arguments) {
        // ラムダ本体を再帰的に Fragment 展開したコードを返す
        // new { p = p, e = new ValueEnumerable<...>(...) } を構築
    }
}
```

### 実装の流れ（関数式フラグメント生成() の拡張）

```csharp
// 匿名型ラムダの検出時、ラムダ本体にシーケンスLINQ呼び出しがある場合
if (ラムダが匿名型を生成する) {
    if (ラムダ本体にシーケンスLINQ呼び出しを含む) {
        // 部分インライン化：ラムダ本体のLINQを再帰的にFragment化
        // static helper メソッドを生成し、その呼び出しに置き換える
        fragment = new InlinedSequenceFunc {
            // ...
        };
    } else if (ラムダ本体にスカラーLINQ呼び出しを含む) {
        // 既存の InlinedScalarFunc を利用
        fragment = new InlinedScalarFunc { ... };
    } else {
        // 純粋なdelegate保持
        fragment = new AnonymousDelegateFuncFragment { ... };
    }
}
```

### 3段階の実装計画

| 段階 | 内容 | 内側のシーケンスLINQ | 備考 |
|------|------|---------------------|------|
| **第1段階（初期実装）** | 純粋なdelegate保持 | `System.Linq.Enumerable` 呼び出し（非Fragment） | まずすべての匿名型LINQがEnumerator遅延評価で動作 |
| **第2段階** | スカラーLINQの部分インライン化（既存のInlinedScalarFuncを活用） | スカラーのみ Fragment化、シーケンスは委譲 | `p.Any()` 等のみインライン化 |
| **第3段階** | シーケンスLINQの部分インライン化 | すべて Fragment化 | 完全インライン化 |

第1段階のみでも匿名型を含む全LINQメソッドが Enumerator で遅延評価されるようになるため、**第2段階・第3段階は後日追加**で問題ない。

## このアプローチの利点

1. **すべての Fragment が自動的に対応**：FuncFragment を引数に取る **すべての Fragment** が追加コード不要で delegate保持に対応
2. **Fragment 側の変更ゼロ**：条件分岐は Fragment.cs の基底メソッドで完結。各 Fragment.cs に1行も変更不要
3. **コード生成パスが単一**：SupportsFlatLoop/GetFlatLoopCode/FuncAssignFlat/FuncInvokeFlat が不要になりメンテナンス性向上
4. **将来の Fragment 追加にも追従**：新しい Fragment が FuncFragment を引数に取るだけで自動的に匿名型対応
5. **段階的移行可能**：interceptor.cs の特殊パスは削除しないまま、Fragment 側の delegate保持を有効化できる。その後 FlatLoop を削除

## 実装ステップ

1. FuncFragment / Func.cs に `CanInvokeAsDelegate` 系プロパティを追加
2. `AnonymousDelegateFuncFragment` クラスを追加
3. `関数式フラグメント生成()` に匿名型検出分岐を追加
4. Fragment.cs の `FuncInvoke`, `FuncAssign`, `FuncAssignCurrent`, `FuncInvokeCondition`, `FuncAssignFlat` に `CanInvokeAsDelegate` チェックを追加
5. Fragment.cs の `ClosureXxx` メソッド群にデリゲートフィールド/コンストラクタの自動収集を追加
6. Fragment.cs の `DefineEnumeratorStructCore` がデリゲートフィールドを出力するよう修正
7. Fragment.cs から `SupportsFlatLoop`, `GetFlatLoopCode()`, `FuncAssignFlat()`, `FuncInvokeFlat()` を削除（ただし当面は残置）
8. 各 Fragment（Join.cs, GroupJoin.cs 等）から `GetFlatLoopCode()` 実装を削除（ただし当面は残置）
9. interceptor.cs から `匿名型Select生成53`, `匿名型GroupBy生成54`, `匿名型LINQ実行文追加52`, および FlatLoop 関連分岐を削除
10. `匿名型警告Descriptor`（SEAMLESS001）は維持
11. 全テスト通過確認

## 注意点

- **型名を書いてはいけない**: 生成コード上で匿名型の型名（`<>f__AnonymousType0`）が絶対に出現しないこと
- **ValueEnumerable の型引数**: `ValueEnumerable<Enumerator<TEnum>, TResult>` の `TResult` が匿名型になるが、これはメソッドの型推論で解決されるので問題なし
- **既存テストの後方互換**: 匿名型を含まない LINQ の生成コードは全く変わらないこと（CanInvokeAsDelegate が false のまま）
- **閉包変数 + デリゲートの共存**: キャプチャ変数ありのラムダでも delegate保持可能。閉包変数は従来通り struct closure フィールド、デリゲート呼び出しは `this._del.Invoke(item)` で共存
- **GroupBy も Enumerator 統一の対象**: BufferedSequence として既に Enumerator パスを持っているため、delegate保持モードにすることで interceptor.cs の特殊パスなしで動作する
- **部分インライン化は第2段階/第3段階**: 内側のLINQまでFragment展開するのは後日の拡張としても、第1段階だけでも匿名型LINQ全体が Enumerator 遅延評価で動作する
