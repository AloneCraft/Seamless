# ゼロアロケーション・アーキテクチャとリファクタリング

Seamless FLinq が達成した **真のゼロアロケーション（0 bytes）** の原理と、イテレータ構文を高速化する Roslyn リファクタリングメニューについて解説します。

---

## 1. なぜ「手前素通し」ではゼロアロケーションにならないのか？

C# コンパイラは、メソッドチェーン（例: `array.Where(x => x > threshold).Count()`）を実行する際、**内側の引数から順番に先行評価** します。

```csharp
// ユーザーのコード
public int RunQuery(int[] array, int threshold)
{
    return array.Where(x => x > threshold).Select(x => x * 2).Count();
}
```

もし `Where` や `Select` を「中身は何もしない素通し（Pass-Through）メソッド」に差し替えたとしても、呼び出し手前の評価ステップで **キャプチャ用クロージャ（`DisplayClass`）とデリゲートインスタンス（合計 88 バイト）が必ずヒープに確保** されてしまいます。

| 方式 | 100,000 件実行時のメモリ | 実行時間 (μs) | 原理 |
| :--- | :---: | :---: | :--- |
| **標準 `System.Linq`** | **192 B** | 550 μs | 中間イテレータ ＋ クロージャ |
| **手前素通し (Pass-Through)** | **88 B** | 60 μs | ラムダ引数先行評価によるクロージャ残存 |
| **手書き Ideal ループ** | **0 B** | 53 μs | 完全ゼロアロケーション |
| **Seamless FLinq (新仕様)** | **0 B** | **71 μs** | **Caller 全自動置換により式評価ごと消滅** |

---

## 2. Caller 全自動インターセプトの仕組み

Seamless FLinq では、C# 12 / .NET 10 の **Roslyn Interceptors** を進化させ、**メソッドの呼び出し側（Caller）単位で全自動置換** を行います。

1. **ユーザーコード完全無変更（1文字も変えない）**:
   属性（`[LinqIntercept]`）や `partial` キーワードの追記は一切不要です。
2. **呼び出し箇所ごとの最適化インライン展開**:
   呼び出し元（`Main`、APIコントローラ、サービスクラス、テスト等）をインターセプトし、パイプライン全体を単一の融合ループ（Fused Loop）へ差し替えます。
3. **クロージャの完全消滅**:
   元の LINQ メソッド本体（`Where(...)` 式）自体が物理的に実行されないため、クロージャもデリゲートも 1 つも生成されず、**完全 0 bytes** を達成します。

---

## 3. 例外的な構文とフォールバック警告（`FLINQ004`）

C# の仕様上、以下の構文を含むメソッドは単一のインライン融合ループに展開できません：

- `yield return` / `yield break`（同期イテレータ）
- `await` / `IAsyncEnumerable<T>`（非同期ストリーム）

これらのメソッドが存在する場合、Seamless はプログラムの正常動作を保証するために **標準の `System.Linq` へ安全にフォールバック** します。
このとき、コンパイラおよび IDE 上に以下の **黄色警告アイコン（`FLINQ004`）** が表示され、最適化されていない箇所を見逃しません：

```text
warning FLINQ004: 'CreateInputs' は yield return または await を含んでいるため、標準の System.Linq へ安全にフォールバックしました。ゼロアロケーション化するには展開リファクタリングメニュー等の使用を検討してください。
```

---

## 4. IDE リファクタリング機能（ゼロアロケーション構造体展開）

フォールバック警告（`FLINQ004`）が出たメソッドをゼロアロケーション化したい場合、Seamless の **Roslyn Code Refactoring Provider** を使用できます。

Visual Studio や VS Code、Rider 上で対象メソッドにカーソルを置き、**`Ctrl + .`（または `Alt + Enter`）** を押すと、電球メニューが表示されます：

- 同期イテレータ: **「Seamless: ゼロアロケーション構造体列挙子へ展開」**
- 非同期ストリーム: **「Seamless: ゼロアロケーション非同期構造体列挙子へ展開」**

### 展開されるコードの例（カプセル化されたネスト構造体）

```csharp
public class DataProcessor
{
    // メソッドの戻り値型が値型構造体へ変更（呼び出し側の foreach や var と完全互換）
    public GenerateNumbersEnumerable GenerateNumbers(int count)
    {
        return new GenerateNumbersEnumerable(count);
    }

    // ヒープ割り当てを一切行わない専用構造体
    public readonly struct GenerateNumbersEnumerable : IEnumerable<int>
    {
        private readonly int _count;
        public GenerateNumbersEnumerable(int count) => this._count = count;
        public GenerateNumbersEnumerator GetEnumerator() => new GenerateNumbersEnumerator(this._count);
        IEnumerator<int> IEnumerable<int>.GetEnumerator() => this.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }

    public struct GenerateNumbersEnumerator : IEnumerator<int>
    {
        private int _state;
        private int _current;
        private readonly int _count;
        private int _i;

        public GenerateNumbersEnumerator(int count) { ... }
        public int Current => this._current;
        public bool MoveNext()
        {
            // インライン状態遷移
            while (this._i < this._count)
            {
                var val = this._i++;
                if (val % 2 == 0) { this._current = val; return true; }
            }
            return false;
        }
        public void Dispose() {}
        public void Reset() => throw new NotSupportedException();
    }
}
```

このリファクタリングを適用することで、`foreach` または `await foreach` でループした際、**ヒープ確保が完全 0 bytes** となり、極限の実行速度を得ることができます。
