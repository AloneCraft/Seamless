# はじめに (Getting Started)

Seamless は、.NET 向けの高パフォーマンスなインメモリ／永続化コレクション、ゼロアロケーション LINQ（FLinq）、および型付きインメモリ/ハイブリッド データベース ライブラリです。

「**メモリに載り切らない膨大なデータであっても、.NET 標準のコレクション・LINQ・C# クラスの書き味のまま、極限の性能でストレージへ分散して扱える**」ことを目指して設計されています。

---

## 主な特徴

- 🚀 **C# の限界を引き出す極限性能**:
  Roslyn Source Generator と C# 12+ Interceptor、Unsafe、`Span<T>`、MemoryPack を組み合わせ、ヒープアロケーションと仮想呼び出しオーバーヘッドを限界まで排除します。
- ⚡ **ゼロアロケーション LINQ (FLinq)**:
  `System.Linq` のメソッド呼び出しをコンパイル時に検出し、最適化されたフラットなループ構造へ直接置き換えます。
- 💾 **ハイブリッド・ストレージ**:
  RAM 内のインメモリコレクション、メモリマップトファイルによる追記型コレクション（`Persisted`）、Microsoft Tsavorite / Garnet をバックエンドとするキーバリューストア（`Tsavorite`）を透過的に使い分けられます。
- 🛡️ **リソース予算管理と安全性**:
  `ExecutionScope` と `ResourceBudget` により、管理対象のメモリ上限およびディスク上限を厳密に制御し、Out Of Memory (OOM) やディスク溢れによるプロセスクラッシュを防ぎます。
- 🌐 **ユニバーサル（サーバー・クライアント共通）**:
  データモデルやクエリコードを一切書き換えることなく、サーバーサイド（ASP.NET Core）からクライアントサイド（Unity, .NET MAUI, デスクトップ）まで全く同じ C# コードを動作させることができます。

---

## インストール

Seamless は GitHub Releases より NuGet パッケージ形式（`.nupkg`）で配布されています。

```bash
# ローカル NuGet ソースの追加（ダウンロードしたパッケージフォルダを指定）
dotnet nuget add source <nupkgフォルダ> --name seamless-local

# プロジェクトへパッケージを追加
dotnet add package Seamless --version 0.1.0
```

---

## 5分で体験する Seamless

### 1. 超高速 LINQ (FLinq)

Seamless を参照したプロジェクトでは、特別なメソッド名を呼ぶ必要はありません。通常の `System.Linq` のコードを書くだけで、コンパイラが自動的に最適化展開を行います。

```csharp
using System.Linq;

// 通常の配列やリストに対する LINQ 呼び出し
int[] numbers = Enumerable.Range(1, 1_000_000).ToArray();

// コンパイル時に Interceptor が介入し、中間オブジェクトを作らない直接ループへ展開されます
int sum = numbers
    .Where(x => x % 2 == 0)
    .Select(x => x * 2)
    .Sum();

Console.WriteLine($"偶数の2倍の合計: {sum}");
```

### 2. 永続化コレクションの利用

プロセスを再起動してもデータを維持したい場合、`Seamless.Collections.Persisted` または `Tsavorite` コレクションを使用します。

```csharp
using Seamless.Collections.Tsavorite;

// ディスク上のストレージフォルダを指定して Dictionary を作成
using var store = new DictionaryStore<long, string>("./data/my_dictionary");
var dict = store.GetOrCreateDictionary("users");

// 通常の IDictionary と同様の操作（データは自動的にハイブリッドログへ永続化）
dict[1001] = "Alice";
dict[1002] = "Bob";

if (dict.TryGetValue(1001, out var name))
{
    Console.WriteLine($"ユーザー 1001: {name}");
}
```

---

## 次のステップ

- [型付きDBアクセス入門](typed-database-guide.md) : `DatabaseContainer` によるスキーマ定義と ACID トランザクション
- [FLinq の詳細](flinq-quickstart.md) : コンパイル時最適化の仕組みとベンチマーク比較
- [サーバー・クライアント共通設計](universal-architecture.md) : 同一のデータコードをマルチプラットフォームで共有する実践
- [永続化ストレージの選び方](persistence-guide.md) : `InMemory`, `Persisted`, `Tsavorite` の特性比較
