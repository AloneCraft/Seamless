using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using MemoryPack;
using Seamless.Database;
using Seamless.Database.Client;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Native;
using Seamless.Application.Schema;

namespace Seamless.Application.ServerSchema;

public static class TemplateEntryPoint{
    public static async Task<int> RunAsync(string[] args){
        if(args.Any(arg=>arg is not "--self-test")){
            Console.Error.WriteLine("使用方法: Server [--self-test]。このテンプレートはローカルのメモリDBを起動します。");
            return 2;
        }
        var options=DatabaseContainerOptions.InMemory with{IndexSize=128,PageSize=4096,SegmentSize=65536,MemorySize=65536};
        using var database=new ApplicationDatabase(options);
        var registry=new DatabaseRegistry();
        var fingerprint=NativeWireContract.For<Schema.Records.Item>();
        registry.Register("application",new InProcessDatabaseAdapter(database),schemaFingerprint:fingerprint);
        var version=typeof(INativeQueryModule).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
        var definition=new NativeQueryDefinition(nameof(IApplicationQueries.Read),"application",fingerprint,"long",typeof(Schema.Records.Item).FullName!){ArgumentsContract=NativeWireContract.For<long>(),ResultContract=fingerprint};
        using var server=new NativeQueryServer(registry,0);
        server.Register(new("application","","","",version,[definition]),new Module(),version);
        server.Start();
        try{
            if(args.Contains("--self-test")){
                var row=new Schema.Records.Item(2,7,3);
                database.Items.Add(row);
                var restored=MemoryPackSerializer.Deserialize<Schema.Records.Item>(MemoryPackSerializer.Serialize(row));
                if(restored.Id!=7||restored.Kind!=2||restored.State!=3){throw new InvalidOperationException("生成プロパティの値が一致しません。");}
                if(Unsafe.SizeOf<Schema.Records.Item>()!=16){throw new InvalidOperationException("byte,long,byteの配置が16バイトになっていません。");}
                if(Unsafe.SizeOf<Schema.Legacy.Records.Item>()!=24||typeof(Schema.Legacy.Records.Item).GetNestedType("F",BindingFlags.NonPublic) is not null){throw new InvalidOperationException("未指定の既存スキーマの配置が変化しています。");}
                var storage=typeof(Schema.Records.Item).GetNestedType("F",BindingFlags.NonPublic)??throw new InvalidOperationException("生成されたFがありません。");
                var names=storage.GetFields(BindingFlags.Public|BindingFlags.Instance).OrderBy(field=>field.MetadataToken).Select(field=>field.Name).ToArray();
                if(!names.SequenceEqual(new[]{"Id","Kind","State"})){throw new InvalidOperationException("Fのフィールド順が最適化されていません。");}
                var mixedStorage=typeof(Schema.Records.MixedValue).GetNestedType("F",BindingFlags.NonPublic)!;
                var mixedNames=mixedStorage.GetFields(BindingFlags.Public|BindingFlags.Instance).OrderBy(field=>field.MetadataToken).Select(field=>field.Name).ToArray();
                if(!mixedNames.SequenceEqual(new[]{"Id","Optional","Kind","Text","Name"})){throw new InvalidOperationException("値・参照を含む値型・参照の分類が一致しません。");}
                var context=AssemblyLoadContext.GetLoadContext(typeof(TemplateEntryPoint).Assembly)!;
                var clientAssembly=context.LoadFromAssemblyName(new("Seamless.Application.ClientSchema"));
                var smoke=clientAssembly.GetType("Seamless.Application.ClientSchema.ClientSmoke",throwOnError:true)!.GetMethod("RunAsync")!;
                await(Task)smoke.Invoke(null,[server.Port,fingerprint])!;
                Console.WriteLine("PASS: generated properties, optimized F, MemoryPack and generated native client RPC");
                return 0;
            }
            Console.WriteLine($"Seamless template: localhost:{server.Port} / application / 空のメモリDB。Ctrl+Cで停止します。");
            var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            ConsoleCancelEventHandler cancel=(_,eventArgs)=>{eventArgs.Cancel=true;completion.TrySetResult();};
            Console.CancelKeyPress+=cancel;
            try{await completion.Task;}finally{Console.CancelKeyPress-=cancel;}
            return 0;
        }finally{await server.StopAsync();}
    }

    private sealed class Module:INativeQueryModule{
        public IEnumerable<byte[]> Execute(string queryId,ReadOnlyMemory<byte> arguments,INativeQueryContext context){
            if(queryId!=nameof(IApplicationQueries.Read)){throw new InvalidOperationException("公開されていない操作です。");}
            var minimum=MemoryPackSerializer.Deserialize<long>(arguments.Span);
            var queries=new ApplicationQueries((ApplicationDatabase)context.Database);
            foreach(var row in queries.Read(minimum)){
                context.CancellationToken.ThrowIfCancellationRequested();
                yield return MemoryPackSerializer.Serialize(row);
            }
        }
    }
}
