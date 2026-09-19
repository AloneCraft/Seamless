using System.Reflection;

namespace Test.Seamless.Database;

public sealed class MaterializedViewTests {
    [Fact,Trait("Category","Unit")]
    public void MaterializedView_EnumeratesRowsAndFindsThemByKeyWithoutPublicMutation() {
        using var database=new TestDatabase();

        Assert.True(database.Add(new(1,10)));
        Assert.True(database.Add(new(2,20)));
        Assert.False(database.Add(new(2,99)));

        Assert.Equal(2,database.Rows.Count);
        Assert.Equal([new Row(1,10),new Row(2,20)],database.Rows.OrderBy(static row=>row.Id));
        Assert.Equal(new(2,20),database.Rows[new(2)]);
        Assert.True(database.Rows.ContainsKey(new(1)));
        Assert.True(database.Rows.TryGetValue(new(2),out var row));
        Assert.Equal(new(2,20),row);
        Assert.False(database.Rows.TryGetValue(new(3),out _));

        var type=database.Rows.GetType();
        Assert.Empty(type.GetConstructors(BindingFlags.Instance|BindingFlags.Public));
        Assert.DoesNotContain(type.GetMethods(BindingFlags.Instance|BindingFlags.Public),static method=>
            method.Name is "Add" or "Remove" or "Clear");
    }

    [Fact,Trait("Category","Unit")]
    public void MaterializedView_AddParticipatesInTransactionOverlay() {
        using var database=new TestDatabase();

        using(var transaction=database.BeginTransaction()) {
            Assert.True(database.Add(new(1,10)));
            Assert.Single(database.Rows);
            Assert.True(database.Rows.ContainsKey(new(1)));
        }
        Assert.Empty(database.Rows);

        using(var transaction=database.BeginTransaction()) {
            Assert.True(database.Add(new(2,20)));
            transaction.Commit();
        }
        Assert.Equal(new Row(2,20),database.Rows[new(2)]);
    }

    private readonly record struct Key(int Id);
    private readonly record struct Row(int Id,int Value);

    private sealed class TestDatabase:global::Seamless.Database.DatabaseContainer {
        public global::Seamless.Database.MaterializedView<Key,Row> Rows=>
            this.MaterializedView<Key,Row>(static row=>new(row.Id));

        public bool Add(Row row)=>AddMaterializedViewRow(this.Rows,row);
    }
}
