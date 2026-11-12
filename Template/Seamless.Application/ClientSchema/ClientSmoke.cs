using MemoryPack;
using Seamless.Database.Client;
using Seamless.Application.Schema.Records;

namespace Seamless.Application.ClientSchema;

public static class ClientSmoke{
    public static async Task RunAsync(int port,string fingerprint){
        await using var connection=new NativeQueryClient("127.0.0.1",port);
        var client=new ApplicationQueriesClient(connection,fingerprint);
        var count=0;
        await foreach(var row in client.ReadAsync(7)){
            if(row.Id!=7||row.Kind!=2||row.State!=3){throw new InvalidOperationException("RPCの値が一致しません。");}
            count++;
        }
        if(count!=1){throw new InvalidOperationException("RPCの行数が一致しません。");}
        if(await client.AggregateAsync(7)!=107){throw new InvalidOperationException("生成クライアントの集約結果が一致しません。");}
        if(await client.InsertAsync(8)!=2){throw new InvalidOperationException("生成クライアントの更新結果が一致しません。");}
        try{await client.FailAsync(9);throw new InvalidOperationException("更新例外がありません。");}
        catch(NativeQueryException error)when(error.Message=="rollback"){}
        await using(var transaction=await connection.BeginTransactionAsync("application")){
            var transactional=new ApplicationQueriesClient(transaction,fingerprint);
            if(await transactional.InsertAsync(10)!=3){throw new InvalidOperationException("取引内の更新結果が一致しません。");}
            try{await transactional.FailAsync(11);throw new InvalidOperationException("取引内の更新例外がありません。");}
            catch(NativeQueryException error)when(error.Message=="rollback"){}
            if(await transactional.InsertAsync(12)!=4){throw new InvalidOperationException("失敗した文の取消結果が一致しません。");}
            await transaction.RollbackAsync();
        }
        await using(var transaction=await connection.BeginTransactionAsync("application")){
            var transactional=new ApplicationQueriesClient(transaction,fingerprint);
            if(await transactional.InsertAsync(13)!=3){throw new InvalidOperationException("取引全体の取消結果が一致しません。");}
            await transaction.CommitAsync();
        }
        if(await client.AggregateAsync(0)!=328){throw new InvalidOperationException("取引確定後の集約結果が一致しません。");}
        var mixed=new NamedItem(2,"template",7,3);
        NativeWireValidation.Validate<NamedItem>(MemoryPackSerializer.Serialize(mixed));
        NativeWireValidation.Validate<NamedItem[]>(MemoryPackSerializer.Serialize(new[]{mixed}));
        var restored=MemoryPackSerializer.Deserialize<NamedItem>(MemoryPackSerializer.Serialize(mixed));
        if(restored.Name!=mixed.Name||restored.Id!=mixed.Id||restored.Kind!=mixed.Kind||restored.State!=mixed.State){throw new InvalidOperationException("参照を含むレコードの往復が一致しません。");}
        var legacy=new Schema.Legacy.Records.NamedItem(mixed.Kind,mixed.Name,mixed.Id,mixed.State);
        if(!MemoryPackSerializer.Serialize(mixed).AsSpan().SequenceEqual(MemoryPackSerializer.Serialize(legacy))){throw new InvalidOperationException("物理配置の変更で論理的なformatter順が変化しています。");}
        foreach(var value in new[]{new MixedValue(new("nested"),2,7,42,"name"),new MixedValue(new(null),0,0,null,null)}){
            NativeWireValidation.Validate<MixedValue>(MemoryPackSerializer.Serialize(value));
            var result=MemoryPackSerializer.Deserialize<MixedValue>(MemoryPackSerializer.Serialize(value));
            if(result.Text!=value.Text||result.Kind!=value.Kind||result.Id!=value.Id||result.Optional!=value.Optional||result.Name!=value.Name){throw new InvalidOperationException("参照を含む値型・Nullable・nullの往復が一致しません。");}
        }
        foreach(var value in new[]{new NullableText(new TextValue("nested")),new NullableText(null)}){
            var bytes=MemoryPackSerializer.Serialize(value);NativeWireValidation.Validate<NullableText>(bytes);
            if(MemoryPackSerializer.Deserialize<NullableText>(bytes).Value!=value.Value){throw new InvalidOperationException("参照を含むnullable値の往復が一致しません。");}
        }
    }
}
