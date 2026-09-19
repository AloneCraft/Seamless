# Seamless

ストレージ backed のコレクションと、コンパイル時 LINQ 書き換えを提供する .NET ライブラリ群。

## パッケージ

### ランタイム

| パッケージ | 依存 | 用途 |
|---|---|---|
| `Seamless.Collections` | MemoryPack | コレクション契約 (`ISet` / `IRecord`)、シリアライザ、メモリマップドリスト基盤 |
| `Seamless.Collections.Persisted` | ↑ | ファイル backed の `Set` / `Dictionary` / `AppendOnlyList` |
| `Seamless.Collections.Tsavorite` | ↑基盤 + Microsoft.Garnet | Tsavorite backed の `HashSet` / `Dictionary` / `Set` |
| `Seamless.Linq` | Collections.Persisted / .Tsavorite | value-enumerable な LINQ ランタイム |
| `Seamless.Database` | Collections.Tsavorite | 宣言的データベースコンテナ (`Table` / `View` / `TableFunction`) |
| `Seamless` | 上記すべて | まとめて入れるファサード |

`Seamless.Collections.Persisted` と `Seamless.Collections.Tsavorite` は互いに依存しません。ファイル backed のコレクションだけが必要なら前者を入れれば足り、Tsavorite/Garnet は付いてきません。

### ソースジェネレータ

| パッケージ | 依存 | 用途 |
|---|---|---|
| `Seamless.FLinq` | `Seamless.Linq` | `System.Linq` / `Seamless.Linq` の呼び出しをコンパイル時に展開する |
| `Seamless.Database.Generator` | `Seamless.Database` | `DatabaseContainer` 派生の partial クラスにスキーマメタデータと取り込み配線を生成する |

どちらもランタイムパッケージを依存として引き連れるので、ジェネレータだけを入れれば動きます。

## 使い方

### ファイル backed のコレクション

```csharp
using Seamless.Collections.Persisted;

using var set = Store.CreateSet<string>(@"C:\data\keys.dat");
set.Add("alpha");
Console.WriteLine(set.Contains("alpha"));   // True

using var dict = Store.CreateTempDictionary<int, string>();
dict.Add(7, "seven");
```

`Dispose` が必要な点と、`Set<T>` の `Add` が `void` を返す点が BCL と異なります。

参照を含む型を格納する場合は `[MemoryPackable]` を付けてください。その際は `MemoryPack` を**自分で参照に加える**必要があります。推移依存では MemoryPack のランタイムしか来ず、`[MemoryPackable]` を処理するソースジェネレータは含まれないためです。

```xml
<PackageReference Include="MemoryPack" Version="1.*" />
```

### Tsavorite backed のコレクション

```csharp
using Seamless.Collections.Tsavorite;

var factory = Store.Create(IndexSize: 131072, MemorySize: 64L * 1024 * 1024);

using var store = factory.Create<int>();
using var set = store.Create();
set.Add(1);
Console.WriteLine(set.Contains(1));         // True
```

`HashSet<T>` は `ICollection<T>`、`Dictionary<K,V>` は `System.Collections.Generic.IDictionary<K,V>` と `IReadOnlyDictionary<K,V>` を実装しています。要素数が `int` を超えうるので `GetLongCount()` を用意しています。

`IndexSize` / `PageSize` / `SegmentSize` / `MemorySize` はいずれも 2 の累乗である必要があり、小さすぎるとログ満杯や OOM になります。

## 対象フレームワーク

`net10.0`

## ライセンス

[MIT](LICENSE)

## 現在の実行・検証基盤

- [`Seamless.Execution`](Seamless.Execution/) は実行スコープ、256 MiB／16 GiBの既定共有予算、決定的Codec、一時資源所有を提供します。
- [`Seamless.Linq`](Seamless.Linq/) の `scope.Query(source)` は、外部ソート、退避可能なGroupBy／Join／集合演算、バイト一致の既定等値性を提供します。
- [`Verification.Cli`](Verification.Cli/) と [`Verification.UI`](Verification.UI/) は同じ検証計画、SQLite履歴、固定baseline、replay／resume／Dynamic失敗縮小を使用します。
- 操作方法と比較条件は [Seamless Verification](docs/verification.md)、実行・等値性・DB・移行契約は [Execution、LINQ、Databaseの現在仕様](docs/execution-and-database.md) を参照してください。
