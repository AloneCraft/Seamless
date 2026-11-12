namespace Test.Seamless.Database.Spec;

// 読み書きの取引(tx)の基本の振る舞い。表へは tx からだけ書け、確定するまで外からは見えない。
public sealed class DatabaseTransactionTests {
    [Fact,Trait("Category","Unit")]
    public void Commit_AppliesStagedRows() {
        using var database=new SpecDatabase();
        using(var transaction=database.BeginTransaction()) {
            Assert.True(transaction.Items.Add(new Records.Item(1,"Apple")));
            Assert.Equal("Apple",transaction.Items[new Keys.Item(1)].Name);
            transaction.Commit();
        }

        using var reader=database.BeginRead();
        Assert.Equal("Apple",reader.Items[new Keys.Item(1)].Name);
    }

    [Fact,Trait("Category","Unit")]
    public void DisposeWithoutCommit_RollsBack() {
        using var database=new SpecDatabase();
        using(var transaction=database.BeginTransaction()) {
            Assert.True(transaction.Items.Add(new Records.Item(1,"Apple")));
            Assert.True(transaction.Items.ContainsKey(new(1)));
        }

        using var reader=database.BeginRead();
        Assert.False(reader.Items.ContainsKey(new(1)));
    }

    [Fact,Trait("Category","Unit")]
    public void InnerRollback_AbortsWholeTransaction() {
        using var database=new SpecDatabase();
        using(var outer=database.BeginTransaction()) {
            Assert.True(outer.Items.Add(new Records.Item(1,"Outer")));
            using(var inner=outer.BeginTransaction()) {
                Assert.True(inner.Items.Add(new Records.Item(2,"Inner")));
                inner.Rollback();
            }
            // 子の取消は根全体を取消し専用にするので、根の確定は失敗する。
            Assert.ThrowsAny<Exception>(()=>outer.Commit());
        }

        using var reader=database.BeginRead();
        Assert.Empty(reader.Items);
    }

    [Fact,Trait("Category","Unit")]
    public void MemoryLimit_SpillsChangesToTsavoriteAndCommits() {
        var options=global::Seamless.Database.DatabaseContainerOptions.InMemory with {
            TransactionMemoryLimit=1,
        };
        using var database=new SpecDatabase(options);
        using(var transaction=database.BeginTransaction()) {
            Assert.True(transaction.Items.Add(new Records.Item(1,"One")));
            Assert.True(transaction.Items.Add(new Records.Item(2,"Two")));
            Assert.Equal(2,transaction.Items.AsEnumerable().Count());
            transaction.Commit();
        }

        using var reader=database.BeginRead();
        Assert.Equal("One",reader.Items[new Keys.Item(1)].Name);
        Assert.Equal("Two",reader.Items[new Keys.Item(2)].Name);
    }

    [Fact,Trait("Category","Unit")]
    public void TemporaryStorageLimit_ReportsItsOwnResourceKind() {
        var options=global::Seamless.Database.DatabaseContainerOptions.InMemory with {
            ManagedMemoryLimitBytes=16L<<20,
            TemporaryStorageLimitBytes=0,
            TransactionMemoryLimit=1,
        };
        using var database=new SpecDatabase(options);
        using var transaction=database.BeginTransaction();
        var error=Assert.Throws<global::Seamless.Execution.ResourceLimitException>(
            ()=>transaction.Items.Add(new Records.Item(1,"One")));
        Assert.Equal(global::Seamless.Execution.ResourceLimitKind.TemporaryStorage,error.Kind);
    }
}
