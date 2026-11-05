using Seamless.Database;
using Seamless.Database.Client;
using Seamless.Database.Gateway.Native;
using Seamless.Database.Gateway.Hosting;
using Seamless.Application.Schema;

namespace Seamless.Application.ServerSchema;

public static class LambdaSmoke{
    public static async Task RunAsync(){
        using var database=new ApplicationDatabase(DatabaseContainerOptions.InMemory with{IndexSize=128,PageSize=4096,SegmentSize=65536,MemorySize=65536});
        var permit=true;
        var schema=new NativeLambdaSchema<IApplicationDatabase>(db=>new ApplicationDatabaseSurface((ApplicationDatabase)db),_=>permit);
        using var runtime=new NativeLambdaDatabase<IApplicationDatabase>(schema);
        var generation="one";
        var client=new NativeLambdaClient<IApplicationDatabase>(runtime.Local(database,()=>generation));

        var insert=client.DefineQuery<long,int>(static(db,id)=>{
            db.Items.Add(new Schema.Records.Item(2,id,3));
            var count=db.Items.Count();
            return count;
        });
        await insert.PrepareAsync();
        if(database.Items.Count!=0||runtime.ExecutionCount!=0||runtime.CompilationCount!=1){throw new InvalidOperationException("PrepareAsyncが実行したか準備を再利用できません。");}
        if(await insert.ExecuteAsync(7)!=1||await insert.ExecuteAsync(8)!=2||runtime.CompilationCount!=1){throw new InvalidOperationException("準備済みブロック更新を再利用できません。");}
        var total=await client.ExecuteAsync<long,long>(7,static(db,min)=>db.Items.Where(row=>row.Id>=min).Sum(row=>row.Id));
        if(total!=15){throw new InvalidOperationException("自動準備と集約の値が一致しません。");}

        var failing=client.DefineQuery<long,int>(static(db,id)=>{
            db.Items.Add(new Schema.Records.Item(2,id,3));
            throw new InvalidOperationException("rollback");
        });
        try{await failing.ExecuteAsync(9);throw new InvalidOperationException("例外がありません。");}
        catch(InvalidOperationException error)when(error.Message=="rollback"){}
        if(database.Items.ContainsKey(new(9))){throw new InvalidOperationException("ブロックの失敗がロールバックされません。");}

        permit=false;
        try{await insert.ExecuteAsync(10);throw new InvalidOperationException("権限拒否がありません。");}
        catch(NativeLambdaException error)when(error.Code=="Permission"){}
        if(database.Items.ContainsKey(new(10))){throw new InvalidOperationException("拒否された更新が実行されました。");}
        permit=true;generation="two";
        if(await insert.ExecuteAsync(11)!=3||runtime.CompilationCount!=4){throw new InvalidOperationException("実行前の失効で再準備されません。");}
        await RunNetworkAsync();
    }

    private static async Task RunNetworkAsync(){
        using var database=new ApplicationDatabase(DatabaseContainerOptions.InMemory with{IndexSize=128,PageSize=4096,SegmentSize=65536,MemorySize=65536});
        var registry=new DatabaseRegistry();registry.Register("application",new InProcessDatabaseAdapter(database),schemaFingerprint:"network-one");
        var permit=true;
        using var server=new NativeQueryServer(registry,0);
        server.RegisterLambda("application",new NativeLambdaSchema<IApplicationDatabase>(db=>new ApplicationDatabaseSurface((ApplicationDatabase)db),_=>permit));server.Start();
        await using var connection=new NativeQueryClient("127.0.0.1",server.Port);
        var client=connection.Lambda<IApplicationDatabase>("application");
        var insert=client.DefineQuery<long,int>(static(db,id)=>{
            db.Items.Add(new Schema.Records.Item(1,id,2));
            var count=db.Items.Count();return count;
        });
        await Task.WhenAll(insert.PrepareAsync(),insert.PrepareAsync());
        if(database.Items.Count!=0){throw new InvalidOperationException("通信の準備でデータが変更されました。");}
        if(await insert.ExecuteAsync(1)!=1||await insert.ExecuteAsync(2)!=2){throw new InvalidOperationException("通信の準備済み更新が一致しません。");}
        var combined=await client.ExecuteAsync<long,long>(0,static(db,min)=>{
            var selected=db.Items.Where(row=>row.Id>=min);
            var count=selected.Count();var sum=selected.Sum(row=>row.Id);return count*100+sum;
        });
        if(combined!=203){throw new InvalidOperationException("複数の集約の値が一致しません。");}
        permit=false;
        try{await insert.ExecuteAsync(3);throw new InvalidOperationException("通信の権限拒否がありません。");}
        catch(NativeLambdaException error)when(error.Code=="Permission"&&error.ExecutionNotStarted){}
        permit=true;
        await using(var transaction=await connection.BeginTransactionAsync("application")){
            var transactional=transaction.Lambda<IApplicationDatabase>();
            var add=transactional.DefineQuery<long,int>(static(db,id)=>{db.Items.Add(new Schema.Records.Item(1,id,2));return db.Items.Count();});
            var fail=transactional.DefineQuery<long,int>(static(db,id)=>{db.Items.Add(new Schema.Records.Item(1,id,2));throw new InvalidOperationException("rollback");});
            if(await add.ExecuteAsync(4)!=3){throw new InvalidOperationException("外側の取引へ参加できません。");}
            try{await fail.ExecuteAsync(5);throw new InvalidOperationException("通信の例外がありません。");}
            catch(NativeLambdaException error)when(error.Code=="Execution"&&error.Message=="rollback"){}
            if(await add.ExecuteAsync(6)!=4){throw new InvalidOperationException("失敗した文だけのロールバックができません。");}
            await transaction.CommitAsync();
        }
        if(database.Items.Count!=4||database.Items.ContainsKey(new(3))||database.Items.ContainsKey(new(5))){throw new InvalidOperationException("通信の最終状態が一致しません。");}
        await using(var transaction=await connection.BeginTransactionAsync("application")){
            var transactional=transaction.Lambda<IApplicationDatabase>();
            await transactional.ExecuteAsync<long,bool>(7,static(db,id)=>db.Items.Add(new Schema.Records.Item(1,id,2)));
            await transaction.RollbackAsync();
        }
        if(database.Items.ContainsKey(new(7))){throw new InvalidOperationException("外側の取消が反映されません。");}
        await server.StopAsync();
    }
}
