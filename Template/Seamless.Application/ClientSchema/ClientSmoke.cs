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
        var lambdas=connection.Lambda<Schema.IApplicationDatabase>("application");
        var aggregates=lambdas.DefineQuery<long,long>(static(db,min)=>{
            var rows=db.Items.Where(row=>row.Id>=min);
            var count=rows.Count();var sum=rows.Sum(row=>row.Id);return count*100+sum;
        });
        await aggregates.PrepareAsync();
        if(await aggregates.ExecuteAsync(7)!=107){throw new InvalidOperationException("クライアント専用DLLからのラムダ実行が一致しません。");}
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
