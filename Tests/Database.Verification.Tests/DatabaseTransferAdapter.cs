using Seamless.Database;
using Seamless.Database.Rdbms;

namespace Database.Verification.Tests;

public sealed class DatabaseTransferAdapter{
    [Fact]public async Task ReadRowsAsync(){
        using var database=new Fixture();
        using(var transaction=database.BeginTransaction()){
            for(var id=0;id<4;id++){database.Items.Add(new(id));}
            transaction.Commit();
        }
        var table=new NativeTable("dbo","Items","Fixture.Items",[new("Id","System.Int32",false)],["Id"]);
        {
            var values=new List<int>();
            await foreach(var row in global::Seamless.Database.Transfer.DatabaseTransferAdapter.ReadRowsAsync(database,table)){
                values.Add((int)row[0]!);await Task.Yield();
            }
            Assert.Equal([0,1,2,3],values.Order().ToArray());
        }
        {
            await foreach(var row in global::Seamless.Database.Transfer.DatabaseTransferAdapter.ReadRowsAsync(database,table)){await Task.Yield();break;}
        }
        {
            using var cancellation=new CancellationTokenSource();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>{
                await foreach(var row in global::Seamless.Database.Transfer.DatabaseTransferAdapter.ReadRowsAsync(database,table,cancellation.Token)){cancellation.Cancel();await Task.Yield();}
            });
        }
        {
            var count=0;
            await foreach(var row in global::Seamless.Database.Transfer.DatabaseTransferAdapter.ReadRowsAsync(database,table)){count++;}
            Assert.Equal(4,count);
            using var write=database.BeginTransaction();database.Items.Add(new(4));write.Commit();
        }
    }

    private readonly record struct Row(int Id);
    private sealed class Fixture():DatabaseContainer(DatabaseContainerOptions.InMemory){
        [ImportedDatabaseObject("dbo","Items")]
        public Table<Row,Row>Items=>this.Table<Row,Row>(row=>row);
    }
}
