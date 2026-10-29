# C#の性能限界を引き出す技術 (C# to the Metal)

「C# は開発生産性に優れるが、C や C++ と比べると遅い」——かつて語られていたこの定説は、近年の .NET ランタイムと言語機能の進化によって過去のものとなりつつあります。

Seamless は、**「C# のハイレベルな表現力を保ったまま、ベアメタル（ハードウェアの生の計算力）の性能限界まで到達する」** ことを設計目標に掲げています。そのために駆使されている極限技術を解説します。

---

## 1. Roslyn C# 12+ Interceptors (コンパイル時直接置換)

C# 12 で導入された Interceptor（インターセプター）は、特定のメソッド呼び出しをコンパイル時に別のメソッド実装へと完全にすり替える機構です。

```csharp
// ユーザーが書くコード
var total = list.Where(x => x > 0).Sum();

// コンパイラが生成するインターセプター
[InterceptsLocation("UserFile.cs", line: 42, character: 15)]
public static long InlinedWhereSum(this List<int> list, Func<int, bool> predicate)
{
    // メソッド呼び出し・オブジェクト確保を一切行わない直接ループ
    long sum = 0;
    var span = CollectionsMarshal.AsSpan(list);
    ref var ptr = ref MemoryMarshal.GetReference(span);
    for (int i = 0; i < span.Length; i++)
    {
        int val = Unsafe.Add(ref ptr, i);
        if (val > 0) sum += val;
    }
    return sum;
}
```

これにより、ユーザーは美しい LINQ 構文を維持しながら、実行時には C 言語で書かれたポインタ演算ループと同等のアセンブリ命令を実行させることができます。

---

## 2. Unsafe, `Span<T>`, and `MemoryMarshal`

マネージドな配列アクセスには、言語仕様として配列境界チェック（Bounds Check）が伴います。通常は JIT コンパイラが境界チェックを除去しますが、複雑なネストループではチェックが残ることがあります。

Seamless では、ホットパスにおいて以下の Unsafe 技法を適切にカプセル化して適用しています：

- **`CollectionsMarshal.AsSpan(List<T>)`**:
  `List<T>` の内部バッファを一切コピーせず、ゼロコストで `Span<T>` として取得。
- **`MemoryMarshal.GetReference(Span<T>)` & `Unsafe.Add`**:
  配列インデクサの境界チェックを完全にバイパスし、生の CPU レジスタによるポインタオフセット加算（間接参照）へとコンパイル。
- **`ref struct` の活用**:
  スタック上にのみ存在する `ref struct` を駆使することで、ヒープへの意図しないエスケープ（ボクシング）をコンパイル時に物理的に遮断。

---

## 3. MemoryPack: シリアライゼーションの究極形

データの永続化やプロセス間通信において、JSON や汎用バイナリシリアライザ（MessagePack 等）は、フィールド名や型タグの解析、可変長整数デコードなど、膨大な CPU 命令を消費します。

Seamless は、C# 最速のシリアライザである **MemoryPack** を基盤に採用しています：

- **ゼロ・エンコーディング**:
  Blittable 型であれば、型のメモリレイアウト（C 構造体と同じバイト並び）をそのままバイナリとして書き込みます。デシリアライズは単なるポインタキャスト（`MemoryMarshal.Cast`）となり、デコード時間は実質「0 ナノ秒」です。
- **Source Generator による直列化コード事前生成**:
  リフレクションを 1 つも使用せず、各フィールドへの読み書きがストレートな代入文としてコンパイルされます。

---

## 4. SIMD & Hardware Intrinsics (ベクトル演算)

CPU のベクトル命令（AVX2, AVX-512, ARM Neon）を活用し、複数のデータ（例: 8個の 32bit 整数）を 1 サイクルで同時に比較・集計します。

```text
[ 通常のスカラー処理 (1要素ずつ処理) ]
Item 0 -> CMP -> ADD
Item 1 -> CMP -> ADD
Item 2 -> CMP -> ADD

[ AVX-512 ベクトル処理 (16要素を同時処理) ]
[ 16個の整数 ] -> vcmpps -> vpaddd -> 1クロックで完了
```

Seamless のデータ構造は、メモリが常に 64 バイト境界でキャッシュアラインされるよう配置されているため、SIMD 命令による並列演算の恩恵を 100% 受けることができます。

---

## 関連項目

- [先行の構造体LINQを超えた理由](beyond-struct-linq.md) : ループ融合の優位性
- [ハードウェア適合と NUMA / Affinity](hardware-and-numa.md) : CPU キャッシュとメモリアクセスの最適化
- [コレクション内部構造](../architecture/data-structures.md) : Blittable メモリ配置
