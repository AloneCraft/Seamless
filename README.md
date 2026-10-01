# Seamless

ストレージ backed のコレクションと、コンパイル時 LINQ 書き換えを提供する .NET ライブラリ群。

## パッケージ

### ランタイム

| パッケージ | 依存 | 用途 |
|---|---|---|
| `Seamless.Collections` | MemoryPack | コレクション契約 (`ISet` / `IRecord`)、シリアライザ、Persisted リストのファイル入出力基盤 |
| `Seamless.Collections.Persisted` | ↑ + Seamless.Execution | ファイル backed の `Set` / `Dictionary` / `AppendOnlyList` |
| `Seamless.Collections.Tsavorite` | ↑基盤 + Seamless.Execution + Microsoft.Garnet | Tsavorite backed の `HashSet` / `Dictionary` / `Set` |
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

一通りの機能を自動で動かして見せるサンプルアプリ(MAUI)が [Samples/Sample.NetShop](Samples/Sample.NetShop/README.md) にあります。

### 共通ストアから作る（推奨）

保存場所・寿命・メモリとディスクの上限は `Seamless.Execution.Store` が持ち、コレクションは各ライブラリの拡張メソッドでストアから作ります。デバイスと保存フォルダはストアが管理します。索引などの大きさは既定でストアの種類と上限から自動で決まり、必要な場合は `TsavoriteTuning` で指定できます。

```csharp
using Seamless.Collections.Tsavorite;   // CreateTsavorite / CreateTsavoriteHashSet
using Seamless.Collections.Persisted;   // CreatePersistedSet / CreatePersistedList / CreatePersistedDictionary

// 一時: Store.CreateTemporary()   破棄でフォルダごと削除。メモリ上限を超えた分はディスクへ退避
// 永続: Store.Open(path)          Save() した時点だけが確定し、開き直すとその時点へ戻る
// メモリ: Store.CreateMemory()     ディスクを使わない。上限を超えると ResourceLimitException
using var store = Seamless.Execution.Store.Open(@"C:\data\app", new() { MemoryBytes = 1L << 30, DiskBytes = 64L << 30 });

using var orders = store.CreateTsavoriteHashSet<long>("orders", expectedCount: 10_000_000); // データ本体
using (var set = orders.Create()) {        // 操作用オブジェクト（スレッドごとに作る）
    set.Add(1);
}
var ids = store.CreatePersistedSet<long>("ids");   // Persisted は内部の作業用。件数は呼び出し側が管理する
ids.Add(2);

store.Save();                               // Tsavorite と Persisted をまとめて確定
```

- Tsavorite の件数はデータ本体が管理します。`Create()` の操作用オブジェクトは追加・削除のたびに件数を増減し、並列処理用の `CreateUncounted()` は増減しないので、集計した差分を `ApplyCountDelta` で反映します。
- 索引は件数に応じて自動で拡張します。ただし複数の操作用オブジェクトが同時に生きている間は拡張しないので、並列で大量に入れる場合は `expectedCount` を渡してください。
- 等価判定はシリアライズ後のバイト列の一致です。
- Persisted はファイルを使うため、メモリストアでは作れません。
- Tsavorite のデータ本体を置くフォルダは、完全パスを約 250 文字以内にしてください。Garnet の制限で、超えると `TsavoriteException`("Path ... is too long")になります(一時ストアも同じ)。Persisted は影響を受けません。空白・日本語・絵文字を含むフォルダは使えます。

### 詳細設定と観測

`CreateTsavorite`・`CreateTsavoriteHashSet`・`CreateTsavoriteSet` は `(name, TsavoriteTuning tuning, long expectedCount = 0)` を受け取ります。大きさはバイト単位の正の2の累乗です。`IndexSize` は64バイト以上、`PageSize` は4096バイト〜1 GiB、`SegmentSize` はページ以上、`LogMemorySize` はページ2つ分以上、`MutableFraction` は0より大きく1以下にします。

```csharp
using Seamless.Collections;
using Seamless.Collections.Tsavorite;
using Seamless.Collections.Persisted;

using var store = Seamless.Execution.Store.CreateTemporary();
using var data = store.CreateTsavoriteHashSet<long>("measured", new TsavoriteTuning {
    IndexSize = 4096,
    PageSize = 4096,
    SegmentSize = 1L << 20,
    LogMemorySize = 65536,
    AutoGrowIndex = false,
    Storage = new(FileAccess.ReadWrite, StorageAccessPattern.Random, StorageBufferingMode.Buffered)
});
using (var set = data.Create()) {
    for (long i = 0; i < 1000; i++) { set.Add(i); }
}
var statistics = data.GetStatistics();
Console.WriteLine($"buckets={statistics.IndexSize}, overflow={statistics.OverflowBucketCount}");
Console.WriteLine($"memory log={statistics.MemoryLogBytes}, disk log={statistics.DiskLogBytes}");
// 必要な場合だけ呼ぶ。索引全体の分布を文字列にする。
Console.WriteLine(data.DumpIndexDistribution());

// Persisted の Unbuffered は Windows の直接 I/O。Buffered / Unbuffered 間で Save/Load の契約と保存形式は同じ。
var list = store.CreatePersistedList<long>("cold", tuning: new StorageTuning(
    FileAccess.ReadWrite, StorageAccessPattern.Sequential, StorageBufferingMode.Unbuffered));
list.Add(42);
store.Save();
```

- `AutoGrowIndex` の既定値は `true`。`false` では索引を固定し、保存済み索引と明示指定の大きさが違う場合は `InvalidOperationException` で拒否します。省略時の自動決定と、複数アクセサ時の拡張禁止は維持します。
- `Storage` の既定値は `null` で、従来のデバイスを使います。Tsavorite が反映する項目は `AccessPattern` と `BufferingMode` です。メモリストアへ `Storage` は指定できません。
- 統計の `IndexSize` は64バイトのバケット数、`IndexBytes` はそのバイト数です。`EntryCount` は索引の使用枠数で、コレクション件数ではありません。`OverflowBucketCount` は初期予約を含み、`OverflowBucketAllocations` は初期予約を除く割当数です。
- `MemoryLogBytes = TailAddress - HeadAddress`、`DiskLogBytes = HeadAddress - BeginAddress` は論理ログの範囲を表します。ファイルの確保容量やオブジェクトログを含む物理ディスク使用量ではありません。並行書込み中の統計項目間の整合性は保証しません。
- `GetStatistics()` の `EntryCount` と `DumpIndexDistribution()` は索引全体と溢れバケットを走査するため、高頻度の監視には向きません。
- Persisted の `Set`・`List`・`Dictionary` は末尾の `tuning` 引数で設定できます。Buffered はメモリマップ、Unbuffered は Windows の `FILE_FLAG_NO_BUFFERING` と整列バッファによる直接 I/O を使います。データ・位置索引・ハッシュ索引・AppendOnly は両 I/O 方式で同じ保存形式を使い、方式を変えて開き直せます。非WindowsのPersisted Unbufferedは `PlatformNotSupportedException` で拒否します。

### クエリ内部の作業コレクション

`CreatePersistedSortedSet` / `CreatePersistedSortedDictionary` / `CreatePersistedSortedList` は一括追加後に `CompleteAdding()` で確定します。確定前のキー検索は例外となり、確定後は比較順の列挙と二分探索を使います。`SortedList` は JOIN 等に必要な重複行を保持します。

`CreatePersistedHashSet` / `CreatePersistedHashDictionary` は追加中にも検索でき、クエリの等価規則を指定できます。両方式とも共通 Store が寿命・予算・保存世代を所有します。既存 API のバイト等価性は維持します。ファイル上のハッシュ索引 `.hindex` は新形式を使い、旧形式の索引は読み込まず明確に拒否します。この拒否を理由とする自動移行や削除は行いません。API 対応、状態、比較器、保存互換性と利用例は [クエリ一時作業コレクション](docs/クエリ一時作業コレクション.md) を参照してください。

### ファイル backed のコレクション（旧 API）

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

### Tsavorite backed のコレクション（旧 API）

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
