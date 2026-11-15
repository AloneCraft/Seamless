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
                var row=new Schema.Tables.Item(new(7),2,3);
                // DB の更新は取引の中で行う(暗黙の取引は廃止した)。
                using(var transaction=database.BeginTransaction()){transaction.Items.Add(row);transaction.Commit();}
                var restored=MemoryPackSerializer.Deserialize<Schema.Tables.Item>(MemoryPackSerializer.Serialize(row));
                if(restored.Id!=7||restored.Kind!=2||restored.State!=3){throw new InvalidOperationException("生成プロパティの値が一致しません。");}
                // キー(long)と、キー以外の参照を含まない列の入れ物 S(byte,byte)で16バイト。
                if(Unsafe.SizeOf<Schema.Tables.Item>()!=16){throw new InvalidOperationException("byte,long,byteの配置が16バイトになっていません。");}
                if(!Fields(typeof(Schema.Tables.Item),"S").SequenceEqual(new[]{"Kind","State"})){throw new InvalidOperationException("Sのフィールド順が一致しません。");}
                // キー以外の列を、参照を含まない入れ物 S(大きい順)と、参照・参照を含む値型の入れ物 C に分ける。
                if(!Fields(typeof(Schema.Tables.MixedValue),"S").SequenceEqual(new[]{"Optional","Kind"})||!Fields(typeof(Schema.Tables.MixedValue),"C").SequenceEqual(new[]{"Text","Name"})){throw new InvalidOperationException("値・参照を含む値型・参照の分類が一致しません。");}
                var context=AssemblyLoadContext.GetLoadContext(typeof(TemplateEntryPoint).Assembly)!;
                var clientAssembly=context.LoadFromAssemblyName(new("Seamless.Application.ClientSchema"));
                var smoke=clientAssembly.GetType("Seamless.Application.ClientSchema.ClientSmoke",throwOnError:true)!.GetMethod("RunAsync")!;
                await(Task)smoke.Invoke(null,[server.Port,fingerprint])!;
                Console.WriteLine("PASS: separated S/C storage, MemoryPack, exported native package RPC, generated aggregation, transactions and rollback");
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

    private static string[] Fields(Type record,string group)=>
        (record.GetNestedType(group,BindingFlags.NonPublic)??throw new InvalidOperationException("生成された"+group+"がありません。"))
            .GetFields(BindingFlags.Public|BindingFlags.Instance).OrderBy(field=>field.MetadataToken).Select(field=>field.Name).ToArray();

}
