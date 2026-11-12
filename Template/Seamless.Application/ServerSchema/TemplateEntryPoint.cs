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
        if(args.Length==2&&args[0]=="--export-native"){
            var path=NativePrebuiltPackage.Publish(args[1],new ApplicationQueriesModule(),[NativeDatabaseAssembly.From<ApplicationDatabase>("application")]);
            Console.WriteLine("Native artifact: "+path);return 0;
        }
        if(args.Any(arg=>arg is not "--self-test")){
            Console.Error.WriteLine("使用方法: Server [--self-test] または Server --export-native <出力フォルダー>。このテンプレートはローカルのメモリDBを起動します。");
            return 2;
        }
        var options=DatabaseContainerOptions.InMemory with{IndexSize=128,PageSize=4096,SegmentSize=65536,MemorySize=65536};
        using var database=new ApplicationDatabase(options);
        var registry=new DatabaseRegistry();
        var fingerprint=NativeDatabaseAssembly.From<ApplicationDatabase>("application").SchemaFingerprint;
        registry.Register("application",new InProcessDatabaseAdapter(database),schemaFingerprint:fingerprint);
        var exported=NativePrebuiltPackage.Read(Path.Combine(AppContext.BaseDirectory,"native","native-build.json"));
        using var published=new LoadedNativeQueryModule(exported,new Dictionary<string,Assembly>{{"application",typeof(ApplicationDatabase).Assembly}});
        var version=typeof(INativeQueryModule).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
        using var server=new NativeQueryServer(registry,0);
        server.Register(exported.Manifest,published.Module,version);
        server.Start();
        try{
            if(args.Contains("--self-test")){
                if(!ReferenceEquals(published.Module.GetType().Assembly,typeof(ApplicationDatabase).Assembly)
                    ||exported.Manifest.Queries.Any(query=>query.SchemaFingerprint!=fingerprint)){
                    throw new InvalidOperationException("ビルド時のNativeメタデータと実行中のDBが一致しません。");
                }
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
                Console.WriteLine("PASS: optimized F, MemoryPack, exported native package RPC, generated aggregation, transactions and rollback");
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

}
