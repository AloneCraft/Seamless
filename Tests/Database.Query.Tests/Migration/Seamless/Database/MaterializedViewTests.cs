using System.Reflection;

namespace Test.Seamless.Database.MaterializedViews;

// 生成器の目印。この名前空間のコンテナに取引・読み込みの入口を生成させる。
internal static partial class DatabaseGenerator;

public readonly record struct MaterializedViewKey(int Id);
public readonly record struct MaterializedViewRow(int Id,int Value);

// 実体化ビューの行は取込(ImportTables)でだけ入り、公開の書き込み口を持たない。取込は取引に参加する。
public sealed partial class MaterializedViewTestDatabase:global::Seamless.Database.DatabaseContainer {
    public global::Seamless.Database.MaterializedView<MaterializedViewKey,MaterializedViewRow> Rows=>
        this.MaterializedView<MaterializedViewKey,MaterializedViewRow>(static row=>new(row.Id));
}

public sealed class MaterializedViewTests {
    private sealed class Source(params MaterializedViewRow[] rows):global::Seamless.Database.IDatabaseImportSource {
        public IEnumerable<TRecord> ReadTable<TRecord>(global::Seamless.Database.System.Keys.DatabaseObject key)=>
            key.Name=="Rows" ? (IEnumerable<TRecord>)(object)rows : [];
    }

    [Fact,Trait("Category","Unit")]
    public void MaterializedView_EnumeratesRowsAndFindsThemByKeyWithoutPublicMutation() {
        using var database=new MaterializedViewTestDatabase();
        database.ImportTables(new Source(new(1,10),new(2,20)));

        using var reader=database.BeginRead();
        Assert.Equal(2,reader.Rows.Count);
        Assert.Equal([new MaterializedViewRow(1,10),new MaterializedViewRow(2,20)],reader.Rows.AsEnumerable().OrderBy(static row=>row.Id));
        Assert.Equal(new(2,20),reader.Rows[new(2)]);
        Assert.True(reader.Rows.ContainsKey(new(1)));
        Assert.True(reader.Rows.TryGetValue(new(2),out var row));
        Assert.Equal(new(2,20),row);
        Assert.False(reader.Rows.TryGetValue(new(3),out _));

        var type=database.Rows.GetType();
        Assert.Empty(type.GetConstructors(BindingFlags.Instance|BindingFlags.Public));
        Assert.DoesNotContain(type.GetMethods(BindingFlags.Instance|BindingFlags.Public),static method=>
            method.Name is "Add" or "Remove" or "Clear");
    }

    [Fact,Trait("Category","Unit")]
    public async Task MaterializedView_ImportParticipatesInTheTransaction() {
        using var database=new MaterializedViewTestDatabase();

        await using(var transaction=await database.BeginTransactionAsync()) {
            await transaction.ImportTablesAsync(new Source(new MaterializedViewRow(1,10)));
            Assert.True(transaction.Rows.ContainsKey(new(1)));
        }
        using(var reader=database.BeginRead()) {
            Assert.Empty(reader.Rows);
        }

        await using(var transaction=await database.BeginTransactionAsync()) {
            await transaction.ImportTablesAsync(new Source(new MaterializedViewRow(2,20)));
            await transaction.CommitAsync();
        }
        using(var reader=database.BeginRead()) {
            Assert.Equal(new MaterializedViewRow(2,20),reader.Rows[new(2)]);
        }
    }
}
