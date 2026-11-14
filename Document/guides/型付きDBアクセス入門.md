# 型付きDBアクセス入門 (Typed Database Guide)

Seamless の `Seamless.Database` は、C# クラス宣言から完全なスキーマ定義、インデックス、ACID トランザクション、および WAL（Write-Ahead Log）クラッシュリカバリを提供する型付き埋め込みデータベースエンジンです。

---

## データベースコンテナの定義

データベース全体のスキーマは、`DatabaseContainer` を継承したクラスとして宣言的に定義します。トランザクション対象のテーブルは `Table<TKey, TRecord>`（`BeginTransaction` / `BeginRead` 経由でアクセス）、トランザクションを介さない直接操作のテーブルは `Set<TKey, TRecord>`（`db` 直下でアクセス）として宣言します。

```csharp
using Seamless.Database;

public sealed partial class ShopDatabase : DatabaseContainer
{
    public ShopDatabase(DatabaseContainerOptions options) : base(options) { }

    // 主キーを持つトランザクション対象テーブルの定義
    public Table<long, UserRecord> Users => this.Table<long, UserRecord>(u => u.Id);
    public Table<long, OrderRecord> Orders => this.Table<long, OrderRecord>(o => o.OrderId);
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
// トランザクションの開始（読み書き・常に Serializable）
using (var tx = db.BeginTransaction())
{
    // データの挿入・更新（トランザクションオブジェクト tx 経由でアクセス）
    tx.Users.Add(new UserRecord(1, "Alice", "alice@example.com", 5));
    tx.Orders.Add(new OrderRecord(1001, 1, 15000, DateTime.UtcNow));

    // トランザクションのコミット（Commit を呼ばずに Dispose された場合は自動取消）
    tx.Commit();
    Console.WriteLine("トランザクションが正常にコミットされました。");
}
```

---

## FLinq を用いた型安全なクエリの実践

テーブルに対する読み取りクエリは `using var r = db.BeginRead();`（開始時点のスナップショット読み取り）またはトランザクション `tx`（どちらも `IReader` を実装）を介して記述します。

C# 12+ の Interceptor 設定を有効にしておくことで、これらのメソッド内の LINQ 呼び出しは、コンパイル時に Roslyn Generator によって検出され、中間オブジェクトを生成しない直接ループへと自動展開（ゼロアロケーション化）されます。

```csharp
using System.Collections.Generic;
using System.Linq;

public class ShopRepository
{
    private readonly ShopDatabase db;

    public ShopRepository(ShopDatabase db)
    {
        this.db = db;
    }

    // ① 条件フィルタと射影クエリ
    public List<ActiveUserInfo> GetActiveUsers(int minLevel)
    {
        using var r = this.db.BeginRead();
        // この Where / Select / ToList はコンパイル時にインターセプトされ、
        // テーブルストレージからの直接走査ループへと展開されます
        return r.Users
            .Where(u => u.Level >= minLevel)
            .Select(u => new ActiveUserInfo(u.Id, u.Name))
            .ToList();
    }

    // ② テーブル間の結合（Join）とグループ集計
    public List<UserSalesSummary> GetUserSalesSummaries()
    {
        using var r = this.db.BeginRead();
        // 複数テーブルの Join もパイプライン融合により高速に実行されます
        return r.Users
            .Join(
                r.Orders,
                u => u.Id,
                o => o.UserId,
                (u, o) => new { u.Name, o.Amount })
            .GroupBy(x => x.Name)
            .Select(g => new UserSalesSummary(g.Key, g.Sum(x => x.Amount)))
            .ToList();
    }
}

public record ActiveUserInfo(long Id, string Name);
public record UserSalesSummary(string UserName, long TotalAmount);
```

> [!TIP]
> **インターセプトが正しく適用されているか確認するには？**
> 上記の `.Where(...)` や `.Select(...)` にカーソルを合わせて **F12（定義へ移動）** を押してください。
> 通常の `System.Linq.Enumerable` ではなく、Roslyn が自動生成した `obj/.../generated/` 配下の Interceptor メソッドへジャンプすれば、正しくゼロアロケーション最適化が適用されています。
> 詳しい条件や確認方法は [ゼロアロケーション LINQ (FLinq) 入門](ゼロアロケーションLINQ入門.md) をご覧ください。

---

## 関連項目

- [ゼロアロケーション LINQ (FLinq)](ゼロアロケーションLINQ入門.md) : プロジェクト設定とインターセプト確認法
- [ORマッパーとの根本的相違](../architecture/ORマッパーとの相違.md) : EF Core や Dapper と比較したオーバーヘッドの違い
- [データベースエンジンの内部構造](../architecture/データベースエンジン内部構造.md) : WAL、整合性チェック、クラッシュ復旧のメカニズム
- [サーバー・クライアント共通設計](サーバー・クライアント共通設計.md) : クライアントアプリとサーバーで同一スキーマを共有する方法
