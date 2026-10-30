using Sample.NetShop.Core;
using Sample.NetShop.Core.Shop;
using Seamless.Database.Gateway;
using Seamless.Database.Gateway.Hosting;

namespace Seamless.Database.Server;

public static class Program{
    public static async Task Main(string[]args){
        global::System.Console.OutputEncoding=global::System.Text.Encoding.UTF8;
        Console.WriteLine("=================================================");
        Console.WriteLine("       SeamlessDB Server - Starting...           ");
        Console.WriteLine("=================================================");

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
        Console.WriteLine("サーバーが稼働中です。[Enter] キーを押すと終了します。");

        await Task.Run(()=>Console.ReadLine());

        Console.WriteLine("シャットダウン中...");
        await gateway.StopAsync();
        Console.WriteLine("SeamlessDB Server は正常に終了しました。");
    }
}
