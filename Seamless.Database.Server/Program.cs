using Sample.NetShop.Core;
using Sample.NetShop.Core.Shop;
using Seamless.Database.Gateway;
using Seamless.Database.Gateway.Hosting;

namespace Seamless.Database.Server;

public static class Program{
    public static async Task ServeGenerationAsync(string[] args){
        if(args.Length<2||args[0]!="--generation"){throw new ArgumentException("Usage: --generation <directory> [--stop-file <path>] [--ready-file <path>]");}
        using var host=new ComparisonHost(args[1]);using var gateway=new GatewayServer(host.Databases);
        if(host.Generation.ModuleManifest is not null){gateway.Native.Load(Seamless.Database.Rdbms.ComparisonGeneration.ResolveManagedPath(args[1],host.Generation.ModuleManifest),host.Generation.ProductVersion);}
        string? stop=null,ready=null;for(var i=2;i<args.Length;i+=2){if(i+1>=args.Length){throw new ArgumentException("Missing option value.");}if(args[i]=="--stop-file"){stop=args[i+1];}else if(args[i]=="--ready-file"){ready=args[i+1];}else{throw new ArgumentException("Unknown server option.");}}
        gateway.Start();if(ready is not null){await File.WriteAllTextAsync(ready,global::System.Text.Json.JsonSerializer.Serialize(new{Generation=host.Generation.Id,Pid=Environment.ProcessId,CreatedUtc=global::System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime()}));}
        var completion=new TaskCompletionSource();Console.CancelKeyPress+=(_,e)=>{e.Cancel=true;completion.TrySetResult();};
        if(stop is null){await completion.Task;}else{while(!File.Exists(stop)&&!completion.Task.IsCompleted){await Task.WhenAny(completion.Task,Task.Delay(250));}}
        await gateway.StopAsync();
    }

    public static async Task Main(string[]args){
        global::System.Console.OutputEncoding=global::System.Text.Encoding.UTF8;
        Console.WriteLine("=================================================");
        Console.WriteLine("       SeamlessDB Server - Starting...           ");
        Console.WriteLine("=================================================");

        if(args.Length>0){await ServeGenerationAsync(args);return;}

        // 1. 商店DB を初期化
        var dbFolder=Path.Combine(AppContext.BaseDirectory,"ShopDbData");
        using var db=商店DB.開く(dbFolder,資源設定.標準);
        var inserted=db.初期データを登録する();
        if(inserted){
            Console.WriteLine("[DB] 商店DB: 初期データを登録しました。");
        }else{
            Console.WriteLine("[DB] 商店DB: 既存データをロードしました。");
        }

        // 2. インプロセスアダプターとゲートウェイサーバーを構築
        var adapter=new InProcessDatabaseAdapter(db);
        var gatewayOptions=new GatewayServerOptions{
            TdsPort=14333,
            HttpPort=8088
        };
        using var gateway=new GatewayServer(adapter,gatewayOptions);

        // 3. ゲートウェイ起動
        gateway.Start();
        Console.WriteLine();
        Console.WriteLine("-------------------------------------------------");
        Console.WriteLine(" [TDS サービス] 起動完了 (SQL Server 互換)");
        Console.WriteLine($"   ポート: localhost,{gateway.TdsPort}");
        Console.WriteLine("   認証  : SQL Server 認証 (ユーザー名: sa / パスワード: 任意)");
        Console.WriteLine("   接続先: SSMS / DataGrip / LINQPad / Excel / Access");
        Console.WriteLine("-------------------------------------------------");
        Console.WriteLine(" [HTTP サービス] 起動完了 (REST / Web UI)");
        Console.WriteLine($"   URL   : http://localhost:{gateway.HttpPort}/");
        Console.WriteLine("   API   : http://localhost:{gateway.HttpPort}/api/tables");
        Console.WriteLine("-------------------------------------------------");
        Console.WriteLine();
        Console.WriteLine("サーバーが稼働中です。(Ctrl+C またはプロセス終了で停止)");

        var tcs=new TaskCompletionSource();
        Console.CancelKeyPress+=(s,e)=>{
            e.Cancel=true;
            tcs.TrySetResult();
        };
        AppDomain.CurrentDomain.ProcessExit+=(s,e)=>{
            tcs.TrySetResult();
        };

        await tcs.Task;

        Console.WriteLine("シャットダウン中...");
        await gateway.StopAsync();
        Console.WriteLine("SeamlessDB Server は正常に終了しました。");
    }
}
