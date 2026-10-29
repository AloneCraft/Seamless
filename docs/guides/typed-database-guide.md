# 型付きDBアクセス入門 (Typed Database Guide)

Seamless の `Seamless.Database` は、C# クラス宣言から完全なスキーマ定義、インデックス、ACID トランザクション、および WAL（Write-Ahead Log）クラッシュリカバリを提供する型付き埋め込みデータベースエンジンです。

---

## データベースコンテナの定義

データベース全体のスキーマは、`DatabaseContainer` を継承したクラスとして宣言的に定義します。

```csharp
using Seamless.Database;

public sealed partial class ShopDatabase : DatabaseContainer
{
    public ShopDatabase(DatabaseContainerOptions options) : base(options) { }

    // 主キーを持つテーブルの定義
    public Table<long, UserRecord> Users => this.GetTable<long, UserRecord>();
    public Table<long, OrderRecord> Orders => this.GetTable<long, OrderRecord>();
}

// レコードの定義 (MemoryPack による超高速シリアライズに対応)
public sealed record UserRecord(long Id, string Name, string Email, int Level);
public sealed record OrderRecord(long OrderId, long UserId, long Amount, DateTime CreatedAt);
```

---

## データベースの初期化とオプション

データベースは、完全なインメモリモード、またはディスク永続化（ストレージモード）のいずれかで起動できます。

```csharp
// 1. 完全インメモリモード（テストや揮発性キャッシュ用）
using var inMemoryDb = new ShopDatabase(DatabaseContainerOptions.InMemory);

// 2. ディスク永続化ストレージモード（WAL とクラッシュリカバリを有効化）
var options = new DatabaseContainerOptions
{
    StorageMode = DatabaseStorageMode.Storage,
    StorageRoot = "./database_store",
    ManagedMemoryLimitBytes = 512 * 1024 * 1024,      // 512 MB メモリ予算
    TemporaryStorageLimitBytes = 4L * 1024 * 1024 * 1024 // 4 GB 一時ディスク予算
};

using var db = new ShopDatabase(options);
```

---

## トランザクション（ACID 保証）

Seamless.Database は、直列化可能（Serializable）なトランザクションをサポートしています。トランザクション内の変更は、コミットされるまで他のセッションから隔離され、障害時には WAL（Write-Ahead Log）に基づいて自動回復されます。

```csharp
// トランザクションの開始
using (var tx = db.BeginTransaction())
{
    try
    {
        // データの挿入・更新
        db.Users.Upsert(1, new UserRecord(1, "Alice", "alice@example.com", 5));
        db.Orders.Upsert(1001, new OrderRecord(1001, 1, 15000, DateTime.UtcNow));

        // トランザクションのコミット（WAL をディスクにフラッシュ）
        tx.Commit();
        Console.WriteLine("トランザクションが正常にコミットされました。");
    }
    catch (Exception ex)
    {
        // Commit() を呼ばずに Dispose された場合、自動的にロールバックされます
        tx.Rollback();
        Console.WriteLine($"ロールバックされました: {ex.Message}");
    }
}
```

---

## FLinq を用いた型安全なクエリ

テーブルデータは `IEnumerable<T>` や FLinq によるクエリ式に対応しています。コンパイル時最適化により、大量データのスキャンや結合がゼロアロケーションで実行されます。

```csharp
using System.Linq;

// レベル 3 以上のユーザーを取得
var activeUsers = db.Users
    .Where(u => u.Level >= 3)
    .Select(u => new { u.Id, u.Name })
    .ToList();

// ユーザーごとの注文合計を結合して集計
var userTotals = db.Users
    .Join(
        db.Orders,
        u => u.Id,
        o => o.UserId,
        (u, o) => new { u.Name, o.Amount })
    .GroupBy(x => x.Name)
    .Select(g => new { UserName = g.Key, TotalAmount = g.Sum(x => x.Amount) });
```

---

## 関連項目

- [ORマッパーとの根本的相違](../architecture/orm-vs-typed-database.md) : EF Core や Dapper と比較したオーバーヘッドの違い
- [データベースエンジンの内部構造](../architecture/database-engine.md) : WAL、整合性チェック、クラッシュ復旧のメカニズム
- [サーバー・クライアント共通設計](universal-architecture.md) : クライアントアプリとサーバーで同一スキーマを共有する方法
