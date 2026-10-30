# ゼロアロケーション LINQ (FLinq Quickstart)

`Seamless.FLinq` は、.NET 標準の `System.Linq.Enumerable` に対する呼び出しをコンパイル時に検出し、中間オブジェクトのアロケーションや仮想呼び出しを一切行わない直接ループコードへと置き換える Roslyn Source Generator / Interceptor です。

---

## 1. なぜ FLinq は標準 LINQ より速いのか？

通常の `System.Linq` は、各クエリ演算子（`Where`, `Select`, `Take` など）を呼び出すたびに、以下のオーバーヘッドを発生させます：

1. **反復子オブジェクト（Iterator）のヒープアロケーション**:
   各ステージごとに `WhereSelectEnumerableIterator` などの参照型インスタンスがヒープ上に確保されます。
2. **デリゲート呼び出し（Delegate Call）のオーバーヘッド**:
   ラムダ式（`x => x > 10`）を呼び出すたびに、デリゲートを経由した間接呼び出しコストが生じます。
3. **多段の `MoveNext()` / `Current` 仮想呼び出し**:
   データが下流に流れるたびに、各反復子のインターフェースまたは仮想メソッドを経由します。

### FLinq の解決策

FLinq は、**C# 12+ の Interceptor 機能** を利用して、ユーザーのコードを変更することなく、コンパイル時に呼び出し元をフックします。

```text
【ユーザーが書いたコード】
source.Where(x => x > 0).Select(x => x * 2).Sum()

【FLinq がコンパイル時に生成・置換するコード】
long sum = 0;
foreach (var item in source)
{
    if (item > 0)
    {
        sum += item * 2;
    }
}
return sum;
```

このように、多段のイテレータを **たった1つの平坦なループ** へと自動的に折り畳むため、GC 負荷が完全にゼロ（0バイト）となり、手書きの命令型ループと同等の最高速度を達成します。

---

## 2. 普通のプログラム（Web、アプリ、ゲーム）で FLinq を使う

FLinq はデータベース専用の機能ではありません。**通常のビジネスロジック、コンソールアプリ、ASP.NET Core サービス、Unity のコンポーネント、ゲームループ、バッチ処理など、任意の C# クラス・メソッドで利用可能** です。

### A. プロジェクト設定 (`.csproj`)
C# 12+ の Interceptor 機能を有効化するため、プロジェクトファイルに以下の設定を追加します：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework> <!-- または net8.0 以上 -->

    <!-- FLinq インターセプターを有効化 -->
    <InterceptorsPreviewNamespaces>$(InterceptorsPreviewNamespaces);Seamless.FLinq;Intercept</InterceptorsPreviewNamespaces>
    <!-- 高速ポインタ走査を利用可能にする -->
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Seamless" Version="0.1.0" />
  </ItemGroup>
</Project>
```

### B. 一般的なクラスでの実装例
特別な基底クラスを継承する必要はありません。通常のクラスや静的メソッドで LINQ を記述するだけで自動的にインターセプトされます。

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

// ① 通常のサービスクラス（ASP.NET Core やデスクトップアプリ）
public class OrderService
{
    // 通常のインスタンスメソッド内で LINQ を記述
    public decimal CalculateTotal(List<OrderItem> items)
    {
        // この Enumerable.Where / Sum はコンパイル時に自動的に最適化展開されます
        return items
            .Where(x => x.Quantity > 0)
            .Sum(x => x.Price * x.Quantity);
    }
}

// ② 静的ユーティリティクラスでも同様に動作
public static class MathUtility
{
    public static int SumEvenSquares(int[] numbers)
    {
        return numbers
            .Where(n => n % 2 == 0)
            .Select(n => n * n)
            .Sum();
    }
}

// ③ 複数文に分かれたクエリでも属性不要で完全自動展開
public class ReportService
{
    public int ProcessMultiStatements(int[] array1, int[] array2)
    {
        // 式1: ここで自動的にゼロアロケーション直接ループへ展開！
        var count = array1.Where(x => x > 10).Select(x => x * 2).Count();

        // 式2: 別行のクエリも自動的に個別の直接ループへ展開！
        var sum = array2.Where(x => x < count).Sum();

        return sum;
    }
}

public record OrderItem(string Name, decimal Price, int Quantity);
```

---

## 3. インターセプトが適用される条件とルール

FLinq Incremental Generator は、以下の安全規則に従ってインターセプト箇所を判定します：

- **✅ 対象となるメソッド**:
  - 通常のインスタンスメソッド（`MethodKind.Ordinary`）
  - 通常の静的メソッド（`static` メソッド）
- **❌ 除外されるケース（理由）**:
  - **`virtual`, `abstract`, `override` メソッド**: 派生クラスでの動的ポリモーフィズムや仮想関数テーブルの挙動を破壊しないための安全設計です。
  - **`ref struct` 内のメソッド**: スタック限定制約による制限。
  - **明示的に無効化された箇所**: `[LinqIntercept(false)]` 属性を付与したクラスまたはメソッド。

---

## 4. IDE / Visual Studio でインターセプトを確認する方法

「本当に自分の書いた LINQ がインターセプトされて高速化されているのか？」を確認する 3 つの確実な方法があります。

### 方法①: F12（定義へ移動）で生成コードを確認する（最も簡単・確実）
コード上の `.Where(...)` や `.Select(...)`、`.Sum()` などの LINQ メソッド呼び出しにカーソルを合わせ、**F12（定義へ移動）** を押します。

- **インターセプトされていない場合**:
  .NET ランタイム標準の `System.Linq.Enumerable` クラス（メタデータ画面）が開きます。
- **正しくインターセプトされている場合**:
  Roslyn Generator が生成したキャッシュファイル（`obj/.../generated/.../F__...g.cs`）が開きます。そこには `[InterceptsLocation(1, "...")]` 属性が付与され、**展開された平坦な `for` ループの実装** が直接記述されていることが確認できます。

### 方法②: Visual Studio のエディタ装飾
Visual Studio 2022 (17.7+) や VS Code (C# Dev Kit) では、インターセプトされているメソッド呼び出しに対して、エディタ上で波線やインターセプトを示すアイコン装飾が表示され、ツールチップで置換先が通知されます。

### 方法③: 最適化トレースの出力 (`EmitOptimizationTrace`)
最適化の適用プロセス（パイプライン融合、述語プッシュダウン等）をログとして確認したい場合は、属性を指定します：

```csharp
using Seamless.FLinq;

public class PerformanceCriticalWorker
{
    // 最適化トレースの出力を有効化
    [LinqIntercept(true, EmitOptimizationTrace = true)]
    public int FastProcess(int[] array)
    {
        return array.Where(x => x > 10).Select(x => x * 2).Count();
    }
}
```
ビルド時に、どの Fragment が結合され、どのようなループが合成されたかの完全なステップトレースが出力されます。

---

## 5. 主な対応演算子

FLinq は、.NET 10 で追加された最新の LINQ 演算子を含め、広範な API をカバーしています：

- **フィルタ・変換**: `Where`, `Select`, `SelectMany`, `Cast`, `OfType`
- **集計・縮約**: `Sum`, `Average`, `Min`, `Max`, `Count`, `Aggregate`, `AggregateBy`
- **切り出し・スキップ**: `Take`, `Skip`, `TakeWhile`, `SkipWhile`
- **集合演算**: `Distinct`, `DistinctBy`, `Union`, `Intersect`, `Except`
- **結合・グループ**: `Join`, `GroupJoin`, `GroupBy`, `CountBy`, `LeftJoin`, `RightJoin`
- **ターミナル**: `ToArray`, `ToList`, `ToDictionary`, `ToHashSet`, `First`, `FirstOrDefault`, `Single`

---

## 関連項目

- [先行の構造体LINQを超えた理由](../performance/beyond-struct-linq.md) : なぜ構造体イテレータの連鎖では不十分なのか
- [FLinq 内部設計](../architecture/flinq-internals.md) : Fragment パイプラインと構文木書き換えの詳細
- [C# の性能限界を引き出す](../performance/csharp-to-the-metal.md) : Unsafe, Span, SIMD との連携
