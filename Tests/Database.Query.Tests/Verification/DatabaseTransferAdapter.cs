using Seamless.Database;
using Seamless.Database.Rdbms;
using Database.Verification.Tests.TransferAdapterFixture;

namespace Database.Verification.Tests;

public sealed class DatabaseTransferAdapter{
    [Fact]public async Task ReadRowsAsync(){
        using var database=new Fixture();
        using(var transaction=database.BeginTransaction()){
            for(var id=0;id<4;id++){transaction.Items.Add(new(id));}
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
            using var write=database.BeginTransaction();write.Items.Add(new(4));write.Commit();
        }
    }

}

